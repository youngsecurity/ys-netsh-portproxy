use std::{
    path::PathBuf,
    sync::mpsc::{self, Receiver, Sender},
    thread,
};

use eframe::egui::{self, Color32, RichText};

use ys_netsh_portproxy::{
    app::{
        firewall_rule_id, AppError, IntegrationProbe, PortProxyManager, PrivilegedExecutor,
        ServiceState,
    },
    backup::BackupDocument,
    domain::{
        expand_listen_port_range, Endpoint, FirewallPolicy, ManagedRule, Port, ProxyKind,
        ProxyRule, ReconcileMode,
    },
    integrations::{DockerBackend, DockerStatus, WslStatus},
    protocol::{CommandResult, PrivilegedCommand},
    state::UserState,
    windows::{
        run_docker_action, run_wsl_action, DockerAction, ElevatedHelperClient, RegistryAdapter,
        RegistryReadReport, WindowsProbes, WslAction,
    },
};

struct ViewOptions {
    integrations: bool,
    transfer: bool,
    about: bool,
}

pub struct PortProxyApp {
    rules: Vec<ManagedRule>,
    groups: Vec<String>,
    selected: Option<usize>,
    editor: Option<RuleEditor>,
    diagnostics: Vec<String>,
    ip_helper: ServiceState,
    wsl: WslStatus,
    docker: DockerStatus,
    busy: bool,
    message: String,
    import_path: String,
    export_path: String,
    sort_column: String,
    sort_ascending: bool,
    draft_dirty: bool,
    backup_id: String,
    baseline_rules: Vec<ProxyRule>,
    views: ViewOptions,
    state_path: PathBuf,
    sender: Sender<WorkerResult>,
    receiver: Receiver<WorkerResult>,
}

impl PortProxyApp {
    #[must_use]
    pub fn new(context: &eframe::CreationContext<'_>) -> Self {
        let (sender, receiver) = mpsc::channel();
        configure_style(&context.egui_ctx);
        let state_path = state_path();
        let state = UserState::load(&state_path).unwrap_or_default();
        let mut app = Self {
            rules: state.rules,
            groups: state.groups,
            selected: None,
            editor: None,
            diagnostics: Vec::new(),
            ip_helper: ServiceState::Unknown,
            wsl: WslStatus::default(),
            docker: DockerStatus::default(),
            busy: false,
            message: "Loading Windows state…".to_owned(),
            import_path: String::new(),
            export_path: String::new(),
            sort_column: state.sort_column,
            sort_ascending: state.sort_ascending,
            draft_dirty: state.draft_dirty,
            backup_id: state.last_backup_id,
            baseline_rules: state.baseline_rules,
            views: ViewOptions {
                integrations: true,
                transfer: false,
                about: false,
            },
            state_path,
            sender,
            receiver,
        };
        app.spawn_refresh(context.egui_ctx.clone());
        app
    }

    fn spawn_refresh(&mut self, context: egui::Context) {
        if self.busy {
            return;
        }
        self.busy = true;
        "Refreshing registry, service, WSL, and Docker state…".clone_into(&mut self.message);
        let sender = self.sender.clone();
        thread::spawn(move || {
            let registry = RegistryAdapter::new().read_report();
            let probes = WindowsProbes::new();
            let service = probes.service_state().unwrap_or(ServiceState::Unknown);
            let result = WorkerResult::Refreshed {
                registry,
                service,
                wsl: probes.wsl_status(),
                docker: probes.docker_status(),
            };
            let _ = sender.send(result);
            context.request_repaint();
        });
    }

    fn spawn_apply(&mut self, context: egui::Context) {
        if self.busy {
            return;
        }
        self.busy = true;
        "Waiting for elevation and applying the verified plan…".clone_into(&mut self.message);
        let sender = self.sender.clone();
        let desired = self.rules.clone();
        thread::spawn(move || {
            let manager = PortProxyManager::new(
                RegistryAdapter::new(),
                ElevatedHelperClient::new(),
                WindowsProbes::new(),
            );
            let result = manager
                .apply_desired(&desired, ReconcileMode::Replace)
                .map_err(|error| error.to_string());
            let _ = sender.send(WorkerResult::Applied(result));
            context.request_repaint();
        });
    }

    fn spawn_import(&mut self, path: PathBuf, context: egui::Context) {
        if self.busy {
            return;
        }
        self.busy = true;
        "Importing backup…".clone_into(&mut self.message);
        let sender = self.sender.clone();
        thread::spawn(move || {
            let result = BackupDocument::load(&path).map_err(|error| error.to_string());
            let _ = sender.send(WorkerResult::Imported(result));
            context.request_repaint();
        });
    }

    fn spawn_export(&mut self, path: PathBuf, document: BackupDocument, context: egui::Context) {
        if self.busy {
            return;
        }
        self.busy = true;
        "Exporting backup…".clone_into(&mut self.message);
        let sender = self.sender.clone();
        thread::spawn(move || {
            let result = document
                .save_new(&path)
                .map(|()| "Export completed".to_owned())
                .map_err(|error| error.to_string());
            let _ = sender.send(WorkerResult::Operation(result));
            context.request_repaint();
        });
    }

    fn spawn_local_action(&mut self, action: LocalAction, context: egui::Context) {
        if self.busy {
            return;
        }
        self.busy = true;
        "Running integration action…".clone_into(&mut self.message);
        let sender = self.sender.clone();
        thread::spawn(move || {
            let result = match action {
                LocalAction::Wsl(action) => run_wsl_action(action),
                LocalAction::Docker(action) => run_docker_action(action),
            }
            .map_err(|error| error.to_string());
            let _ = sender.send(WorkerResult::Operation(result));
            context.request_repaint();
        });
    }

    fn spawn_command(&mut self, command: PrivilegedCommand, context: egui::Context) {
        if self.busy {
            return;
        }
        self.busy = true;
        "Waiting for elevation…".clone_into(&mut self.message);
        let sender = self.sender.clone();
        thread::spawn(move || {
            let result = ElevatedHelperClient::new()
                .execute(command)
                .map_err(|error| error.to_string());
            let _ = sender.send(WorkerResult::Command(result));
            context.request_repaint();
        });
    }

    #[allow(clippy::too_many_lines)]
    fn receive_worker_results(&mut self, context: &egui::Context) {
        while let Ok(result) = self.receiver.try_recv() {
            self.busy = false;
            match result {
                WorkerResult::Refreshed {
                    registry,
                    service,
                    wsl,
                    docker,
                } => {
                    let state = UserState {
                        rules: self.rules.clone(),
                        groups: self.groups.clone(),
                        ..UserState::default()
                    };
                    self.rules = state.merge_effective_against(
                        &self.baseline_rules,
                        &registry.rules,
                        self.draft_dirty,
                    );
                    self.baseline_rules.clone_from(&registry.rules);
                    self.sort_rules();
                    self.diagnostics = registry
                        .diagnostics
                        .into_iter()
                        .map(|item| format!("{}: {}", item.source, item.message))
                        .collect();
                    self.ip_helper = service;
                    self.wsl = wsl;
                    self.docker = docker;
                    self.message = if self.draft_dirty {
                        format!(
                            "Loaded {} effective rule(s); preserved pending drafts",
                            registry.rules.len()
                        )
                    } else {
                        format!("Loaded {} effective rule(s)", registry.rules.len())
                    };
                    self.persist_state();
                }
                WorkerResult::Imported(result) => match result {
                    Ok(document) => {
                        let existing: std::collections::BTreeSet<_> =
                            self.rules.iter().map(|item| item.rule.key()).collect();
                        let before = self.rules.len();
                        self.rules.extend(
                            document
                                .rules
                                .into_iter()
                                .filter(|item| !existing.contains(&item.rule.key())),
                        );
                        for group in document.groups {
                            if !self.groups.contains(&group) {
                                self.groups.push(group);
                            }
                        }
                        self.sort_rules();
                        self.draft_dirty = true;
                        self.message = format!(
                            "Imported {} new rule(s); existing duplicates were preserved",
                            self.rules.len() - before
                        );
                        self.persist_state();
                    }
                    Err(error) => self.message = format!("Import failed: {error}"),
                },
                WorkerResult::Applied(result) => match result {
                    Ok(outcome) => {
                        self.draft_dirty = false;
                        if let Some(backup_id) = outcome.backup_id {
                            self.backup_id = backup_id;
                        }
                        self.message = format!(
                            "Applied {} registry change(s); backup {}",
                            outcome.changes.len(),
                            if self.backup_id.is_empty() {
                                "not created"
                            } else {
                                &self.backup_id
                            }
                        );
                        self.persist_state();
                        self.spawn_refresh(context.clone());
                    }
                    Err(error) => {
                        self.message = format!("Operation failed: {error}");
                        self.spawn_refresh(context.clone());
                    }
                },
                WorkerResult::Command(result) => {
                    self.message = match result {
                        Ok(CommandResult::BackupRestored { backup_id }) => {
                            self.backup_id = backup_id;
                            format!("Backup restored; undo backup {}", self.backup_id)
                        }
                        Ok(result) => command_result_message(result),
                        Err(error) => format!("Operation failed: {error}"),
                    };
                    self.persist_state();
                    self.spawn_refresh(context.clone());
                }
                WorkerResult::Operation(result) => {
                    self.message = match result {
                        Ok(message) => message,
                        Err(error) => format!("Operation failed: {error}"),
                    };
                    self.persist_state();
                    self.spawn_refresh(context.clone());
                }
            }
        }
    }

    fn persist_state(&mut self) {
        let state = UserState {
            rules: self.rules.clone(),
            groups: self.groups.clone(),
            sort_column: self.sort_column.clone(),
            sort_ascending: self.sort_ascending,
            draft_dirty: self.draft_dirty,
            last_backup_id: self.backup_id.clone(),
            baseline_rules: self.baseline_rules.clone(),
            ..UserState::default()
        };
        if let Err(error) = state.save(&self.state_path) {
            self.message = format!("Could not save UI metadata: {error}");
        }
    }

    #[allow(clippy::too_many_lines)]
    fn menu_bar(&mut self, ui: &mut egui::Ui, context: &egui::Context) {
        let selected = self.selected.filter(|index| *index < self.rules.len());
        egui::menu::bar(ui, |ui| {
            ui.menu_button("File", |ui| {
                if ui
                    .add_enabled(!self.busy, egui::Button::new("New rule"))
                    .clicked()
                {
                    self.editor = Some(RuleEditor::new());
                    ui.close_menu();
                }
                if ui
                    .add_enabled(!self.busy, egui::Button::new("Refresh from Windows"))
                    .clicked()
                {
                    self.spawn_refresh(context.clone());
                    ui.close_menu();
                }
                if ui
                    .add_enabled(!self.busy, egui::Button::new("Apply pending changes"))
                    .clicked()
                {
                    self.spawn_apply(context.clone());
                    ui.close_menu();
                }
                ui.separator();
                if ui.button("Import / export…").clicked() {
                    self.views.transfer = true;
                    ui.close_menu();
                }
                ui.separator();
                if ui.button("Exit").clicked() {
                    context.send_viewport_cmd(egui::ViewportCommand::Close);
                }
            });
            ui.menu_button("Edit", |ui| {
                if ui
                    .add_enabled(
                        selected.is_some() && !self.busy,
                        egui::Button::new("Edit rule"),
                    )
                    .clicked()
                {
                    let index = selected.expect("enabled only with a selected rule");
                    self.editor = Some(RuleEditor::from_rule(index, &self.rules[index]));
                    ui.close_menu();
                }
                if ui
                    .add_enabled(
                        selected.is_some() && !self.busy,
                        egui::Button::new("Clone rule"),
                    )
                    .clicked()
                {
                    let index = selected.expect("enabled only with a selected rule");
                    self.editor = Some(RuleEditor::clone_rule(&self.rules[index]));
                    ui.close_menu();
                }
                if ui
                    .add_enabled(
                        selected.is_some() && !self.busy,
                        egui::Button::new("Toggle status"),
                    )
                    .clicked()
                {
                    let item = &mut self.rules[selected.expect("enabled only with a selection")];
                    item.enabled = !item.enabled;
                    self.draft_dirty = true;
                    self.persist_state();
                    ui.close_menu();
                }
                if ui
                    .add_enabled(
                        selected.is_some() && !self.busy,
                        egui::Button::new("Remove managed firewall rule"),
                    )
                    .clicked()
                {
                    let index = selected.expect("enabled only with a selected rule");
                    let rule_id = firewall_rule_id(self.rules[index].rule.key());
                    self.spawn_command(
                        PrivilegedCommand::RemoveFirewallRule { rule_id },
                        context.clone(),
                    );
                    ui.close_menu();
                }
                ui.separator();
                if ui
                    .add_enabled(
                        selected.is_some() && !self.busy,
                        egui::Button::new("Delete rule"),
                    )
                    .clicked()
                {
                    self.rules
                        .remove(selected.expect("enabled only with a selected rule"));
                    self.selected = None;
                    self.draft_dirty = true;
                    self.persist_state();
                    ui.close_menu();
                }
            });
            ui.menu_button("View", |ui| {
                ui.checkbox(&mut self.views.integrations, "Integration status");
                ui.checkbox(&mut self.views.transfer, "Import / export");
            });
            ui.menu_button("Help", |ui| {
                if ui.button("About").clicked() {
                    self.views.about = true;
                    ui.close_menu();
                }
            });
        });
    }

    #[allow(clippy::too_many_lines)]
    fn toolbar(&mut self, ui: &mut egui::Ui, context: &egui::Context) {
        let fill = ui.visuals().faint_bg_color;
        let mut sort_changed = false;
        egui::Frame::new()
            .fill(fill)
            .inner_margin(egui::Margin::symmetric(12, 10))
            .corner_radius(6)
            .show(ui, |ui| {
                ui.horizontal_wrapped(|ui| {
                    if ui
                        .add_enabled(!self.busy, egui::Button::new("New rule"))
                        .clicked()
                    {
                        self.editor = Some(RuleEditor::new());
                    }
                    if ui
                        .add_enabled(!self.busy, egui::Button::new("Refresh"))
                        .clicked()
                    {
                        self.spawn_refresh(context.clone());
                    }
                    let selected = self.selected.filter(|index| *index < self.rules.len());
                    if ui
                        .add_enabled(selected.is_some() && !self.busy, egui::Button::new("Edit"))
                        .clicked()
                    {
                        let index = selected.expect("enabled only with a selected rule");
                        self.editor = Some(RuleEditor::from_rule(index, &self.rules[index]));
                    }
                    if ui
                        .add_enabled(selected.is_some() && !self.busy, egui::Button::new("Clone"))
                        .clicked()
                    {
                        let index = selected.expect("enabled only with a selected rule");
                        self.editor = Some(RuleEditor::clone_rule(&self.rules[index]));
                    }
                    if ui
                        .add_enabled(
                            selected.is_some() && !self.busy,
                            egui::Button::new("Toggle status"),
                        )
                        .clicked()
                    {
                        let item =
                            &mut self.rules[selected.expect("enabled only with a selection")];
                        item.enabled = !item.enabled;
                        self.draft_dirty = true;
                        self.persist_state();
                    }
                    if ui
                        .add_enabled(
                            selected.is_some() && !self.busy,
                            egui::Button::new("Delete"),
                        )
                        .clicked()
                    {
                        self.rules
                            .remove(selected.expect("enabled only with a selected rule"));
                        self.selected = None;
                        self.draft_dirty = true;
                        self.persist_state();
                    }
                    ui.separator();
                    if ui
                        .add_enabled(!self.busy, egui::Button::new("Apply changes"))
                        .clicked()
                    {
                        self.spawn_apply(context.clone());
                    }
                });
                ui.add_space(4.0);
                ui.horizontal_wrapped(|ui| {
                    ui.label("Sort");
                    egui::ComboBox::from_id_salt("sort_column")
                        .selected_text(&self.sort_column)
                        .show_ui(ui, |ui| {
                            for column in
                                ["Type", "Listen", "Connect", "Group", "Comment", "Enabled"]
                            {
                                sort_changed |= ui
                                    .selectable_value(
                                        &mut self.sort_column,
                                        column.to_owned(),
                                        column,
                                    )
                                    .changed();
                            }
                        });
                    if ui
                        .button(if self.sort_ascending {
                            "Ascending"
                        } else {
                            "Descending"
                        })
                        .clicked()
                    {
                        self.sort_ascending = !self.sort_ascending;
                        sort_changed = true;
                    }
                    ui.separator();
                    ui.label(format!("{} rules", self.rules.len()));
                    if self.draft_dirty {
                        ui.label(
                            RichText::new("Pending changes")
                                .color(Color32::YELLOW)
                                .strong(),
                        );
                    }
                });
            });
        if sort_changed {
            self.sort_rules();
            self.persist_state();
        }
    }

    fn sort_rules(&mut self) {
        let column = self.sort_column.as_str();
        self.rules.sort_by(|left, right| match column {
            "Type" => left.rule.kind.cmp(&right.rule.kind),
            "Listen" => left.rule.listen.cmp(&right.rule.listen),
            "Connect" => left.rule.connect.cmp(&right.rule.connect),
            "Group" => left.group.cmp(&right.group),
            "Comment" => left.comment.cmp(&right.comment),
            "Enabled" => left.enabled.cmp(&right.enabled),
            _ => left.rule.key().cmp(&right.rule.key()),
        });
        if !self.sort_ascending {
            self.rules.reverse();
        }
        self.selected = None;
    }

    fn rules_table(&mut self, ui: &mut egui::Ui) {
        ui.heading("Port proxy rules");
        ui.add_space(6.0);
        if self.rules.is_empty() {
            ui.label(
                RichText::new("No rules yet. Choose New rule to create your first port proxy.")
                    .color(ui.visuals().weak_text_color()),
            );
            return;
        }
        let mut status_changed = false;
        egui::Grid::new("rules_header")
            .num_columns(8)
            .spacing(egui::vec2(18.0, 8.0))
            .striped(true)
            .show(ui, |ui| {
                for heading in [
                    "Selection",
                    "Status",
                    "Type",
                    "Listen",
                    "Connect",
                    "Group",
                    "Comment",
                    "Firewall",
                ] {
                    ui.strong(heading);
                }
                ui.end_row();
                for index in 0..self.rules.len() {
                    let selected = self.selected == Some(index);
                    let selection_text = if selected { "Selected" } else { "Select" };
                    let mut selection_button = egui::Button::new(selection_text);
                    if selected {
                        selection_button = selection_button.fill(ui.visuals().selection.bg_fill);
                    }
                    if ui
                        .add(selection_button)
                        .on_hover_text("Select this rule for toolbar and Edit menu actions")
                        .clicked()
                    {
                        self.selected = Some(index);
                    }

                    let enabled = self.rules[index].enabled;
                    let (status_text, status_fill, status_help) = if enabled {
                        (
                            RichText::new("● Enabled").color(Color32::WHITE),
                            Color32::from_rgb(32, 116, 72),
                            "Enabled: Apply changes will create or keep this Windows port proxy. Click to disable it in the draft.",
                        )
                    } else {
                        (
                            RichText::new("○ Disabled").color(Color32::WHITE),
                            Color32::from_rgb(100, 104, 112),
                            "Disabled: Apply changes will remove this Windows port proxy but retain its saved details. Click to enable it in the draft.",
                        )
                    };
                    if ui
                        .add_enabled(
                            !self.busy,
                            egui::Button::new(status_text)
                                .fill(status_fill)
                                .min_size(egui::vec2(92.0, 0.0)),
                        )
                        .on_hover_text(status_help)
                        .clicked()
                    {
                        self.rules[index].enabled = !enabled;
                        self.draft_dirty = true;
                        status_changed = true;
                    }

                    let managed = &self.rules[index];
                    ui.label(managed.rule.kind.to_string());
                    ui.label(managed.rule.listen.to_string());
                    ui.label(managed.rule.connect.to_string());
                    ui.label(if managed.group.is_empty() {
                        "—"
                    } else {
                        &managed.group
                    });
                    ui.label(if managed.comment.is_empty() {
                        "—"
                    } else {
                        &managed.comment
                    });
                    ui.label(match managed.firewall {
                        FirewallPolicy::None => "None",
                        FirewallPolicy::DomainAndPrivate => "Private",
                        FirewallPolicy::AllProfiles => "All",
                    });
                    ui.end_row();
                }
            });
        if status_changed {
            self.persist_state();
        }
    }

    #[allow(clippy::too_many_lines)]
    fn status_panel(&mut self, ui: &mut egui::Ui, context: &egui::Context) {
        ui.heading("System status");
        ui.add_space(6.0);
        let docker_is_windows = matches!(self.docker.backend, Some(DockerBackend::Windows));
        let docker_backend = match &self.docker.backend {
            Some(DockerBackend::Windows) => "Windows / Docker Desktop".to_owned(),
            Some(DockerBackend::Wsl { distribution }) => format!("WSL / {distribution}"),
            None => "No Docker CLI detected".to_owned(),
        };
        ui.columns(3, |columns| {
            egui::Frame::group(columns[0].style())
                .inner_margin(egui::Margin::same(12))
                .show(&mut columns[0], |ui| {
                    ui.strong("IP Helper");
                    let (label, color) = match self.ip_helper {
                        ServiceState::Running => ("Running", Color32::LIGHT_GREEN),
                        ServiceState::Stopped => ("Stopped", Color32::LIGHT_RED),
                        ServiceState::StartPending => ("Starting", Color32::YELLOW),
                        ServiceState::StopPending => ("Stopping", Color32::YELLOW),
                        ServiceState::Unknown => ("Unknown", Color32::GRAY),
                    };
                    ui.label(RichText::new(label).color(color).strong());
                    ui.add_space(4.0);
                    ui.horizontal_wrapped(|ui| {
                        if ui
                            .add_enabled(!self.busy, egui::Button::new("Start"))
                            .clicked()
                        {
                            self.spawn_command(PrivilegedCommand::StartIpHelper, context.clone());
                        }
                        if ui
                            .add_enabled(!self.busy, egui::Button::new("Reload"))
                            .clicked()
                        {
                            self.spawn_command(PrivilegedCommand::ReloadIpHelper, context.clone());
                        }
                    });
                });
            egui::Frame::group(columns[1].style())
                .inner_margin(egui::Margin::same(12))
                .show(&mut columns[1], |ui| {
                    ui.strong("Windows Subsystem for Linux");
                    let (status, color) = if self.wsl.available {
                        ("Running", Color32::LIGHT_GREEN)
                    } else {
                        ("Not detected", Color32::GRAY)
                    };
                    ui.label(RichText::new(status).color(color).strong());
                    ui.label(
                        self.wsl
                            .distribution
                            .as_deref()
                            .unwrap_or("No distribution"),
                    );
                    ui.label(format!(
                        "{} listening ports",
                        self.wsl.listening_ports.len()
                    ));
                    ui.add_space(4.0);
                    ui.horizontal_wrapped(|ui| {
                        if ui
                            .add_enabled(!self.busy, egui::Button::new("Start"))
                            .clicked()
                        {
                            self.spawn_local_action(
                                LocalAction::Wsl(WslAction::Start),
                                context.clone(),
                            );
                        }
                        if ui
                            .add_enabled(!self.busy, egui::Button::new("Restart"))
                            .clicked()
                        {
                            self.spawn_local_action(
                                LocalAction::Wsl(WslAction::Restart),
                                context.clone(),
                            );
                        }
                        if ui
                            .add_enabled(!self.busy, egui::Button::new("Shutdown"))
                            .clicked()
                        {
                            self.spawn_local_action(
                                LocalAction::Wsl(WslAction::Shutdown),
                                context.clone(),
                            );
                        }
                    });
                });
            egui::Frame::group(columns[2].style())
                .inner_margin(egui::Margin::same(12))
                .show(&mut columns[2], |ui| {
                    ui.strong("Docker");
                    let (status, color) = if self.docker.running {
                        ("Running", Color32::LIGHT_GREEN)
                    } else if self.docker.available {
                        ("Stopped", Color32::LIGHT_RED)
                    } else {
                        ("Not detected", Color32::GRAY)
                    };
                    ui.label(RichText::new(status).color(color).strong());
                    ui.label(docker_backend);
                    let count = self.docker.containers.len();
                    ui.label(format!(
                        "{count} {}",
                        if count == 1 {
                            "container"
                        } else {
                            "containers"
                        }
                    ));
                    ui.add_space(4.0);
                    if docker_is_windows {
                        ui.horizontal_wrapped(|ui| {
                            for (label, action) in [
                                ("Start", DockerAction::Start),
                                ("Restart", DockerAction::Restart),
                                ("Stop", DockerAction::Stop),
                            ] {
                                if ui
                                    .add_enabled(!self.busy, egui::Button::new(label))
                                    .clicked()
                                {
                                    self.spawn_local_action(
                                        LocalAction::Docker(action),
                                        context.clone(),
                                    );
                                }
                            }
                        });
                    } else if matches!(self.docker.backend, Some(DockerBackend::Wsl { .. })) {
                        ui.small("Docker lifecycle is managed inside WSL.");
                    }
                });
        });
    }

    fn import_export_panel(&mut self, ui: &mut egui::Ui, context: &egui::Context) {
        ui.collapsing("Import / export", |ui| {
            ui.horizontal(|ui| {
                ui.label("Import path");
                ui.text_edit_singleline(&mut self.import_path);
                if ui
                    .add_enabled(!self.busy, egui::Button::new("Import"))
                    .clicked()
                {
                    self.spawn_import(PathBuf::from(self.import_path.trim()), context.clone());
                }
            });
            ui.horizontal(|ui| {
                ui.label("Export path");
                ui.text_edit_singleline(&mut self.export_path);
                if ui
                    .add_enabled(!self.busy, egui::Button::new("Export new"))
                    .clicked()
                {
                    let document = BackupDocument::new(self.rules.clone(), self.groups.clone());
                    self.spawn_export(
                        PathBuf::from(self.export_path.trim()),
                        document,
                        context.clone(),
                    );
                }
            });
            ui.horizontal(|ui| {
                ui.label("Registry backup ID");
                ui.text_edit_singleline(&mut self.backup_id);
                if ui
                    .add_enabled(
                        !self.busy && !self.backup_id.trim().is_empty(),
                        egui::Button::new("Restore backup"),
                    )
                    .clicked()
                {
                    self.spawn_command(
                        PrivilegedCommand::RestoreRegistryBackup {
                            backup_id: self.backup_id.trim().to_owned(),
                        },
                        context.clone(),
                    );
                }
            });
        });
    }

    fn diagnostics_panel(&self, ui: &mut egui::Ui) {
        if !self.diagnostics.is_empty() {
            ui.collapsing(
                RichText::new(format!("Diagnostics ({})", self.diagnostics.len()))
                    .color(Color32::YELLOW),
                |ui| {
                    for diagnostic in &self.diagnostics {
                        ui.label(diagnostic);
                    }
                },
            );
        }
    }

    fn about_window(&mut self, context: &egui::Context) {
        if !self.views.about {
            return;
        }
        egui::Window::new("About Young Security Port Proxy")
            .open(&mut self.views.about)
            .collapsible(false)
            .resizable(false)
            .show(context, |ui| {
                ui.heading("Young Security Port Proxy");
                ui.label(format!("Version {}", env!("CARGO_PKG_VERSION")));
                ui.add_space(8.0);
                ui.label("A native Windows manager for netsh interface portproxy.");
                ui.hyperlink_to(
                    "Project on GitHub",
                    "https://github.com/youngsecurity/ys-netsh-portproxy",
                );
            });
    }

    fn editor_window(&mut self, context: &egui::Context) {
        let Some(mut editor) = self.editor.take() else {
            return;
        };
        let mut open = true;
        let mut save = false;
        let mut cancel = false;
        egui::Window::new(editor.title())
            .open(&mut open)
            .collapsible(false)
            .resizable(true)
            .show(context, |ui| {
                editor.render(ui);
                if let Some(error) = &editor.error {
                    ui.colored_label(Color32::LIGHT_RED, error);
                }
                ui.separator();
                ui.horizontal(|ui| {
                    save = ui.button("Save draft").clicked();
                    cancel = ui.button("Cancel").clicked();
                });
            });
        if save {
            match editor.build() {
                Ok(new_rules) => {
                    if let Some(index) = editor.original_index {
                        if index < self.rules.len() {
                            self.rules.remove(index);
                        }
                    }
                    self.rules.extend(new_rules);
                    self.rules.sort_by_key(|managed| managed.rule.key());
                    self.selected = None;
                    self.draft_dirty = true;
                    self.persist_state();
                    "Draft updated; choose Apply changes to update Windows"
                        .clone_into(&mut self.message);
                    return;
                }
                Err(error) => editor.error = Some(error.to_string()),
            }
        }
        if open && !cancel {
            self.editor = Some(editor);
        }
    }
}

impl eframe::App for PortProxyApp {
    fn update(&mut self, context: &egui::Context, _frame: &mut eframe::Frame) {
        self.receive_worker_results(context);
        egui::TopBottomPanel::top("top").show(context, |ui| {
            self.menu_bar(ui, context);
            ui.separator();
            ui.horizontal(|ui| {
                ui.heading("Young Security Port Proxy");
                ui.label(RichText::new(format!("v{}", env!("CARGO_PKG_VERSION"))).weak());
                if self.busy {
                    ui.spinner();
                }
            });
            ui.add_space(6.0);
            self.toolbar(ui, context);
            ui.add_space(4.0);
        });
        egui::CentralPanel::default().show(context, |ui| {
            egui::ScrollArea::vertical().show(ui, |ui| {
                ui.add_space(4.0);
                egui::Frame::group(ui.style())
                    .inner_margin(egui::Margin::same(14))
                    .show(ui, |ui| {
                        egui::ScrollArea::horizontal().show(ui, |ui| self.rules_table(ui));
                    });
                if self.views.integrations {
                    ui.add_space(14.0);
                    self.status_panel(ui, context);
                }
                if self.views.transfer {
                    ui.add_space(14.0);
                    self.import_export_panel(ui, context);
                }
                self.diagnostics_panel(ui);
            });
        });
        egui::TopBottomPanel::bottom("bottom").show(context, |ui| {
            ui.add_space(3.0);
            ui.horizontal(|ui| {
                if self.busy {
                    ui.spinner();
                }
                ui.label(&self.message);
            });
            ui.add_space(3.0);
        });
        self.editor_window(context);
        self.about_window(context);
    }
}

struct RuleEditor {
    original_index: Option<usize>,
    kind: ProxyKind,
    listen_address: String,
    listen_port: String,
    connect_address: String,
    connect_port: String,
    range_enabled: bool,
    listen_end: String,
    enabled: bool,
    group: String,
    comment: String,
    firewall: FirewallPolicy,
    error: Option<String>,
}

impl RuleEditor {
    fn new() -> Self {
        Self {
            original_index: None,
            kind: ProxyKind::V4ToV4,
            listen_address: "0.0.0.0".to_owned(),
            listen_port: String::new(),
            connect_address: "127.0.0.1".to_owned(),
            connect_port: String::new(),
            range_enabled: false,
            listen_end: String::new(),
            enabled: true,
            group: String::new(),
            comment: String::new(),
            firewall: FirewallPolicy::None,
            error: None,
        }
    }

    fn from_rule(index: usize, managed: &ManagedRule) -> Self {
        let mut editor = Self::clone_rule(managed);
        editor.original_index = Some(index);
        editor
    }

    fn clone_rule(managed: &ManagedRule) -> Self {
        Self {
            original_index: None,
            kind: managed.rule.kind,
            listen_address: managed.rule.listen.address.to_string(),
            listen_port: managed.rule.listen.port.to_string(),
            connect_address: managed.rule.connect.address.to_string(),
            connect_port: managed.rule.connect.port.to_string(),
            range_enabled: false,
            listen_end: managed.rule.listen.port.to_string(),
            enabled: managed.enabled,
            group: managed.group.clone(),
            comment: managed.comment.clone(),
            firewall: managed.firewall,
            error: None,
        }
    }

    fn title(&self) -> &'static str {
        if self.original_index.is_some() {
            "Edit port proxy"
        } else {
            "Add or clone port proxy"
        }
    }

    fn render(&mut self, ui: &mut egui::Ui) {
        egui::Grid::new("rule_editor")
            .num_columns(2)
            .show(ui, |ui| {
                ui.label("Type");
                egui::ComboBox::from_id_salt("kind")
                    .selected_text(self.kind.to_string())
                    .show_ui(ui, |ui| {
                        for kind in [
                            ProxyKind::V4ToV4,
                            ProxyKind::V4ToV6,
                            ProxyKind::V6ToV4,
                            ProxyKind::V6ToV6,
                        ] {
                            ui.selectable_value(&mut self.kind, kind, kind.to_string());
                        }
                    });
                ui.end_row();
                ui.label("Listen address");
                ui.text_edit_singleline(&mut self.listen_address);
                ui.end_row();
                ui.label("Listen port");
                ui.text_edit_singleline(&mut self.listen_port);
                ui.end_row();
                ui.label("Connect address");
                ui.text_edit_singleline(&mut self.connect_address);
                ui.end_row();
                ui.label("Connect port");
                ui.text_edit_singleline(&mut self.connect_port);
                ui.end_row();
                ui.label("Expand range");
                ui.checkbox(&mut self.range_enabled, "Inclusive listen-port range");
                ui.end_row();
                if self.range_enabled {
                    ui.label("Listen end");
                    ui.text_edit_singleline(&mut self.listen_end);
                    ui.end_row();
                }
                ui.label("Enabled");
                ui.checkbox(&mut self.enabled, "Present in effective Windows state");
                ui.end_row();
                ui.label("Group");
                ui.text_edit_singleline(&mut self.group);
                ui.end_row();
                ui.label("Comment");
                ui.text_edit_singleline(&mut self.comment);
                ui.end_row();
                ui.label("Firewall");
                egui::ComboBox::from_id_salt("firewall")
                    .selected_text(match self.firewall {
                        FirewallPolicy::None => "Do not manage",
                        FirewallPolicy::DomainAndPrivate => "Allow Domain + Private",
                        FirewallPolicy::AllProfiles => "Allow all profiles",
                    })
                    .show_ui(ui, |ui| {
                        ui.selectable_value(
                            &mut self.firewall,
                            FirewallPolicy::None,
                            "Do not manage",
                        );
                        ui.selectable_value(
                            &mut self.firewall,
                            FirewallPolicy::DomainAndPrivate,
                            "Allow Domain + Private",
                        );
                        ui.selectable_value(
                            &mut self.firewall,
                            FirewallPolicy::AllProfiles,
                            "Allow all profiles",
                        );
                    });
                ui.end_row();
            });
    }

    fn build(&self) -> Result<Vec<ManagedRule>, AppError> {
        let listen = Endpoint::parse(
            &self.listen_address,
            &self.listen_port,
            self.kind.listen_is_ipv4(),
        )?;
        let connect = Endpoint::parse(
            &self.connect_address,
            &self.connect_port,
            self.kind.connect_is_ipv4(),
        )?;
        let base = ProxyRule::new(self.kind, listen, connect)?;
        let rules = if self.range_enabled {
            expand_listen_port_range(&base, self.listen_end.parse::<Port>()?)?
        } else {
            vec![base]
        };
        Ok(rules
            .into_iter()
            .map(|rule| ManagedRule {
                rule,
                enabled: self.enabled,
                group: self.group.trim().to_owned(),
                comment: self.comment.trim().to_owned(),
                firewall: self.firewall,
            })
            .collect())
    }
}

#[derive(Debug, Clone, Copy)]
enum LocalAction {
    Wsl(WslAction),
    Docker(DockerAction),
}

enum WorkerResult {
    Refreshed {
        registry: RegistryReadReport,
        service: ServiceState,
        wsl: WslStatus,
        docker: DockerStatus,
    },
    Imported(Result<BackupDocument, String>),
    Applied(Result<ys_netsh_portproxy::app::ApplyOutcome, String>),
    Command(Result<CommandResult, String>),
    Operation(Result<String, String>),
}

fn command_result_message(result: CommandResult) -> String {
    match result {
        CommandResult::Probe {
            helper_version,
            elevated,
        } => format!("Helper {helper_version}; elevated={elevated}"),
        CommandResult::Applied {
            change_count,
            backup_id,
        } => format!(
            "Applied {change_count} change(s); backup {}",
            backup_id.as_deref().unwrap_or("not requested")
        ),
        CommandResult::ServiceChanged => "IP Helper command completed".to_owned(),
        CommandResult::FirewallChanged => "Firewall command completed".to_owned(),
        CommandResult::BackupRestored { backup_id } => {
            format!("Registry backup restored; undo backup {backup_id}")
        }
    }
}

fn configure_style(context: &egui::Context) {
    context.style_mut(|style| {
        style.spacing.item_spacing = egui::vec2(10.0, 8.0);
        style.spacing.button_padding = egui::vec2(12.0, 7.0);
        style.spacing.interact_size.y = 30.0;
        style.spacing.window_margin = egui::Margin::same(12);
        style.spacing.indent = 20.0;
    });
}

fn state_path() -> PathBuf {
    let root = std::env::var_os("APPDATA").map_or_else(std::env::temp_dir, PathBuf::from);
    root.join("Young Security")
        .join("ys-netsh-portproxy")
        .join("state.json")
}
