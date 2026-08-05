# region + -- DESCRIPTION -- +

// + ------------------------------------------------------------------------------------------------------------------------------------- +
// |                                                                                                                                       |
// | JSE UTILITIES CLASS                                                                                                                   |
// |                                                                                                                                       |
// | AUTHOR: J. SCOTT ELBLEIN                                                                                                              |
// |                                                                                                                                       |
// | DESCRIPTION:                                                                                                                          |
// |                                                                                                                                       |
// | THis is a general utilities class to provide many common functions, so it's easy to just add it into most of your programs as needed. |
// | It is a 'living' class, meaning it will change often, and as needed.                                                                  |
// |                                                                                                                                       |
// | And that's bout all there is to that. =)                                                                                              |
// |                                                                                                                                       |
// + ------------------------------------------------------------------------------------------------------------------------------------- +

# endregion

#region + -- IMPORTS -- +

using NAudio.Wave;
using NStandard;
using NetFwTypeLib; // Windows Firewall API
using PortProxyGooey;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Media;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using NStandard.Security;

#endregion

#region + -- REQUIREMENTS -- +

// NUGET Packages:
//
// - System.Management
// - System.ServiceProcess.ServiceController
// - NAudio
// - NStandard: https://github.com/zmjack/NStandard

// Network Section requires Unsafe Code flag in compiler (due to Win32API Usage)

#endregion

namespace JSE_Utils {

    public static class Audio {

        #region + -- VAR DECLARATIONS -- +

        internal static readonly ResourceManager resourceManager = new($"{typeof(Program).Namespace}.Properties.Resources", Assembly.GetExecutingAssembly());

        #endregion

        /// <summary>
        /// Plays a sound
        /// </summary>
        /// <param name="strSoundFile">(string) Either a full valid path to a sound file, or the name of one saved in Resources.</param>
        /// <remarks>Async: BackgroundWorker</remarks>
        public static void PlaySound_BGW(string strSoundFileOrName = "", byte[] bytResource = null) {
            // TODO: Need to now add in handling mp3 from resource. (From file is working)
            BackgroundWorker worker = new();

            worker.DoWork += (sender, e) => {

                // Example Usage:
                //
                // From File:     Audio.PlaySound_BGW(@"C:\Some\Path\SoundName.wav");  Or .mp3 as well.
                // From Resource: Audio.PlaySound_BG("SoundName");                     Name of file as saved in your Resources file.

                // Flag for which audio file type we're handling:
                // 0 = wav
                // 1 = mp3
                int intAudioType = 0;

                SoundPlayer wavPlayer = new();
                WaveOutEvent mp3Player = new();
                MediaFoundationReader audioFile = null;

                // Resource streaming
                Stream stream = new MemoryStream(bytResource);

                // Check on the existence of passed file name
                if (File.Exists(strSoundFileOrName)) {

                    // If a file path was passed

                    // Check file extension
                    if (Path.GetExtension(strSoundFileOrName).EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) {

                        // MP3: Just set a flag so we know how to handle it later
                        intAudioType = 1;

                        // Not gonna use the Wav player, so be a good egg and free up it's resources.
                        wavPlayer.Dispose();

                    } else if (Path.GetExtension(strSoundFileOrName).EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) {

                        // WAV: Flag already defaults to 0; no need to set it here.
                        wavPlayer = new SoundPlayer(strSoundFileOrName);

                    }

                //} else if (resourceManager.GetStream(strSoundFileOrName) != null) {
                } else if (resourceManager.GetObject(strSoundFileOrName) != null) {

                    // NOTE: Currently has potential to shit the bed here due to possibly not being a Stream (for .wav),
                    //       but I switched the test above to accomodate .mp3, and currently in a rush to finish the app,
                    //       so haven't added further validation before just attempting to play it.

                    Debug.WriteLine("Obj Type: " + resourceManager.GetObject(strSoundFileOrName));

                    // If not a file path, try to run it from Resources. 
                    if (resourceManager.GetObject(strSoundFileOrName).ToString() == "System.Byte[]") {
                        // Left off here. Merging the tmpplay func.
                        // MP3
                        //audioFile = new MediaFoundationReader(resourceManager.GetStream(strSoundFileOrName));

                    } else if (resourceManager.GetObject(strSoundFileOrName).ToString() == "System.IO.UnmanagedMemoryStreamWrapper") {

                        // WAV
                        wavPlayer = new SoundPlayer(resourceManager.GetStream(strSoundFileOrName));

                        mp3Player.Dispose();
                        audioFile?.Dispose();
                    }

                } else {

                    // No valid sound file was passed along to us; just bail.
                    wavPlayer.Dispose();
                    mp3Player.Dispose();
                    audioFile?.Dispose();
                    return;

                }

                // If all is Go, then try playing it.
                if (intAudioType == 1) {

                    // MP3
                    Debug.WriteLine("Trying to play mp3 file");

                    // Create a WaveOutEvent instance (audio output device)
                    //using WaveOutEvent mp3Player = new();

                    // Create a MediaFoundationReader instance to read the MP3 file
                    //using MediaFoundationReader audioFile = new(strSoundFileOrName);

                    // Create a WaveStream instance for playback
                    // TODO: If audioFile ends up null here; issues ... handle it.
                    WaveStream waveStream = WaveFormatConversionStream.CreatePcmStream(audioFile);

                    // Configure the WaveOutEvent with the WaveStream
                    mp3Player.Init(waveStream);

                    // Start audio playback
                    mp3Player.Play();

                    // Wait until playback is complete. W/out this method being a BGW the GUI would freeze here until the sound finished.
                    while (mp3Player.PlaybackState == PlaybackState.Playing) {
                        System.Threading.Thread.Sleep(100);
                    }

                } else {

                    // WAV

                    try {

                        // Play the sound file
                        wavPlayer.Play();

                        while (!wavPlayer.IsLoadCompleted) {
                            // Keep the method running until the sound finishes playing
                        }

                        wavPlayer.Dispose();

                    } catch (Exception ex) {
                        wavPlayer.Dispose();
                        Debug.WriteLine($"PlaySound(): Error occurred while playing the sound file: {ex.Message}");
                    }

                }

            };

            worker.RunWorkerCompleted += (sender, e) => {

                if (e.Error != null) {
                    // Handle errors
                }

            };

            worker.RunWorkerAsync();
        }

        public static void PlaySound2_BGW(string strSoundFileOrName) {

            BackgroundWorker worker = new();

            worker.DoWork += (sender, e) => {

                // Check if the input is a local file path or a resource name
                bool isLocalFilePath = File.Exists(strSoundFileOrName);

                using (WaveOutEvent waveOut = new()) {

                    WaveStream waveStream;

                    if (isLocalFilePath) {

                        // Play audio from a local file path
                        if (strSoundFileOrName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) {

                            waveStream = new MediaFoundationReader(strSoundFileOrName);

                        } else if (strSoundFileOrName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) {

                            waveStream = new WaveFileReader(strSoundFileOrName);

                        } else {

                            Debug.WriteLine("Unsupported file format.");
                            return;

                        }

                    } else {

                        // Play audio from a resource stream
                        //Stream resourceStream = GetResourceStream(strSoundFileOrName);

                        //if (resourceStream == null) {

                        //    Debug.WriteLine("Resource not found.");
                        //    return;

                        //}

                        //if (resourceStream.CanSeek) {

                        //    waveStream = new WaveFileReader(resourceStream);

                        //} else {

                        //    waveStream = WaveFormatConversionStream.CreatePcmStream(new BlockAlignReductionStream(new WaveFileReader(resourceStream)));

                        //}
                    }

                    //waveOut.Init(waveStream);
                    waveOut.Play();

                    while (waveOut.PlaybackState == PlaybackState.Playing) {
                        System.Threading.Thread.Sleep(100);
                    }
                }

            };

            worker.RunWorkerCompleted += (sender, e) => {

                if (e.Error != null) {
                    // Handle errors
                }

            };

            worker.RunWorkerAsync();
        }

        public static void tmpPlay(byte[] bytResource) {

            Stream stream = new MemoryStream(bytResource);

            Mp3FileReader reader = new(stream);
            WaveOut waveOut = new();

            waveOut.Init(reader);
            waveOut.Play();

        }

    }

    public static class ComboBoxes {

        /// <summary>
        /// Sorts a ComboBox Numerically, rather than the default Albhabetically.
        /// </summary>
        /// <param name="comboBox">A ComboBox object</param>
        public static void SortItemsNumerically(ComboBox comboBox) {

            // Example:
            // ComboBoxes.SortItemsNumerically(comboBox_ListenPort);

            // Make sure built-in sorting is off, or it'll override our work here:
            comboBox.Sorted = false;

            // Step 1: Get the list of items from the ComboBox
            List<string> items = comboBox.Items.Cast<string>().ToList();

            // Step 2: Convert the items to a numeric type (e.g., int or double)
            List<int> numericItems = items.Select(item =>
            {
                if (int.TryParse(item, out int numericValue)) {
                    return numericValue;
                } else {
                    // Handle the case where an item is not a valid numeric value
                    return 0; // or any default value
                }
            }).ToList();

            // Step 3: Sort the items using the numeric values
            numericItems.Sort();

            // Step 4: Clear the ComboBox
            comboBox.Items.Clear();

            // Step 5: Add the sorted items back to the ComboBox
            comboBox.Items.AddRange(numericItems.Select(numericItem => numericItem.ToString()).ToArray());

        }

    }

    public static class Dialogs {

        /// <summary>Creates a custom dialog form so this overall Utils class is more portable</summary>
        /// <param name="strText">The text to show the user</param>
        /// <param name="strTitle">The title text of the form itself</param>
        /// <param name="bShowCancel">[optional Default: true] True = Show Cancel Button, False, OK Button only</param>
        /// <param name="szSize">[optional Default: 500, 400] Size to make the Dialog Window. NOTE: Cannot be smaller than the default size.</param>
        /// <returns>DialogResult.OK on "Ok" button click; DialogResult.Cancel on "Cancel" button or X button click.</returns>
        public static DialogResult CustomDialog(string strText, string strTitle, bool bShowCancel = true, Size szSize = default) {

            // Example Usage(s):
            //
            // Dialogs.CustomDialog("Some message to the user", "Some Title");
            // Dialogs.CustomDialog("Some message to the user", "Some Title", true);
            // Dialogs.CustomDialog("Some message to the user", "Some Title", false, new Size(537, 688));

            // If no Window size was passed, or of they're smaller than the MinimumSize, set it to the Minimum site (for aesthetic reasons)
            if (szSize == default(Size) || szSize.Width < 500 | szSize.Width < 400) {
                szSize = new Size(500, 400);
            }

            DialogResult result = DialogResult.Cancel;

            using (Form form = new()) {

                // Form Props
                form.Text = strTitle;
                form.BackColor = Color.FromArgb(67, 76, 94);
                form.ShowIcon = false;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ShowInTaskbar = false;
                form.FormBorderStyle = FormBorderStyle.FixedSingle;
                //form.FormBorderStyle = FormBorderStyle.Sizable;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.Size = szSize;
                form.MinimumSize = new Size(500, 400);

                if (form.FormBorderStyle == FormBorderStyle.Sizable) {

                    // This is here solely to help determine which form size is wanted when calling the CustomDialog.
                    // Set above to form.FormBorderStyle = FormBorderStyle.Sizable, and then watch the sizes in the Debug window as you resize the form.
                    // Set back  to form.FormBorderStyle = FormBorderStyle.FixedSingle when done.
                    form.Resize += CustomDialog_Resize;

                }

                // + --  Form Controls -- + 

                // TextBox (the place to put any message to the user)
                TextBox txtInfo = new() {
                    AcceptsReturn = false,
                    AcceptsTab = false,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                    AutoSize = false,
                    BackColor = Color.FromArgb(67, 76, 94),
                    BorderStyle = BorderStyle.None,
                    Font = new Font("Microsoft Sans Serif", 12),
                    ForeColor = Color.FromArgb(229, 233, 240),
                    Location = new Point(20, 20),
                    Multiline = true,
                    ReadOnly = true,
                    Size = new Size(form.ClientSize.Width - 10, form.ClientSize.Height - 110),
                    //ScrollBars = ScrollBars.Vertical,
                    TabStop = false,
                    Text = strText,
                    WordWrap = true,
                };

                // Buttons
                if (bShowCancel) {

                    // Only add a Cancel button if user want it there
                    Button btnCancel = new() {
                        Anchor = AnchorStyles.Bottom,
                        BackColor = Color.FromArgb(46, 52, 64),
                        DialogResult = DialogResult.Cancel,
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Microsoft Sans Serif", 12, FontStyle.Bold),
                        ForeColor = Color.FromArgb(191, 97, 106),
                        Location = new Point(form.ClientSize.Width / 2 + 20, form.ClientSize.Height - 70),
                        Size = new Size(80, 50),
                        Text = "Cancel",
                    };

                    form.Controls.Add(btnCancel);

                    // Set the Cancel button as the focused button, essentially acting as the default button
                    form.ActiveControl = btnCancel;

                }

                Button btnOK = new() {
                    Anchor = AnchorStyles.Bottom,
                    BackColor = Color.FromArgb(46, 52, 64),
                    DialogResult = DialogResult.OK,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Microsoft Sans Serif", 12, FontStyle.Bold),
                    ForeColor = Color.FromArgb(235, 203, 139),
                    Location = bShowCancel ? new Point(form.ClientSize.Width / 4 + 20, form.ClientSize.Height - 70) : new Point(form.ClientSize.Width / 3 + 50, form.ClientSize.Height - 70), // Button location depends on whether Cancel button is shown
                    Size = new Size(80, 50),
                    Text = "OK",
                };

                // Add the controls
                form.Controls.Add(txtInfo);
                form.Controls.Add(btnOK);
                
                // Show the form and get the button they clicked
                if (form.ShowDialog() == DialogResult.OK) result = DialogResult.OK;

                // Handle the Resize event
                static void CustomDialog_Resize(object sender, EventArgs e) {

                    // Cast the sender object back to a Form
                    Form form = (Form)sender;

                    // Get the current size of the form
                    Size size = form.Size;

                    // Display the event message along with the current form size
                    Debug.WriteLine($"CustomDialog Form size changed to: w{size.Width} x h{size.Height}");

                }

            }

            // Defaults to false, gets set to true above, only if user clicked the OK button.
            return result;
        }

        /// <summary>
        /// Replicates old school VB InputDialog, for a quick, simple input form.
        /// </summary>
        /// <param name="strInput">Will be placed in the text box if pre-filled; useful for suggestion as well.</param>
        /// <param name="strTitle">Placed on the form's titlebar.</param>
        /// <returns>DialogResult. Also, the input var passed to it will contain the user text (see example).</returns>
        public static DialogResult InputDialog(ref string strInput, string strTitle = "") {

            // Example Usage:
            // 
            // string input = string.Empty;
            //
            // if (Dialogs.InputDialog(ref input, "New name:") == DialogResult.OK && !string.IsNullOrEmpty(input)) {
            //    Debug.WriteLine(input);
            // }
            //
            // input will contain what user entered after it returns.

            Size size = new Size(200, 70);
            Form inputBox = new Form();

            inputBox.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            inputBox.ShowInTaskbar = false;
            inputBox.StartPosition = FormStartPosition.CenterParent;
            inputBox.ClientSize = size;
            inputBox.Text = strTitle;

            TextBox textBox = new TextBox();
            textBox.Size = new Size(size.Width - 10, 23);
            textBox.Location = new Point(5, 5);
            textBox.Text = strInput;
            inputBox.Controls.Add(textBox);

            Button okButton = new Button();
            okButton.DialogResult = DialogResult.OK;
            okButton.Name = "okButton";
            okButton.Size = new Size(75, 23);
            okButton.Text = "&OK";
            okButton.Location = new Point(size.Width - 80 - 80, 39);
            inputBox.Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new Size(75, 23);
            cancelButton.Text = "&Cancel";
            cancelButton.Location = new Point(size.Width - 80, 39);
            inputBox.Controls.Add(cancelButton);

            inputBox.AcceptButton = okButton;
            inputBox.CancelButton = cancelButton;

            DialogResult result = inputBox.ShowDialog();
            strInput = textBox.Text;

            return result;

        }    
    
    }

    public static class Docker {

        /// <summary>
        /// Get Docker Version (async: BackGroundWorker)
        /// </summary>
        /// <param name="bVersionOnly"><c>True:</c> [b version only], else, Full Docker Info.</param>
        /// <returns>Success: (string) Version Number; Fail: Empty string</returns>
        public static void GetInfo_BGW(Action<string> callback, bool bVersionOnly = false) {

            // Example usage(s):
            //
            // Version Only:
            // Docker.GetInfo_BGW((DockerVersion) => Debug.WriteLine($"DOCKER{(!string.IsNullOrEmpty(DockerVersion) ? " (v" + (DockerVersion) + ")" : string.Empty)}: RUNNING"), true);
            //
            // Full:
            // Docker.GetInfo_BGW((DockerInfo) => Debug.WriteLine($"{(DockerInfo)}"));
            //
            // Another:
            //
            // Version Only:
            // Docker.GetInfo_BGW((DockerVersion) => {
            //     Debug.WriteLine($"Docker Version: {DockerVersion}");
            // }, true);

            BackgroundWorker worker = new();

            worker.DoWork += (sender, e) => {

                string strOutput;

                if (bVersionOnly) {

                    // Only fetch the Docker *version*
                    strOutput = Misc.RunCommand("wsl", $"docker version --format {"'{{.Server.Version}}'"}");

                } else {

                    // Fetch the Full Monty
                    // Another way from within WSL: uname -mr && docker version
                    strOutput = Misc.RunCommand("wsl", "docker version");
                    //Debug.WriteLine(strOutput);
                }
                 
                e.Result = !string.IsNullOrEmpty(strOutput) ? strOutput.Trim() : string.Empty;

            };

            worker.RunWorkerCompleted += (sender, e) => {

                if (e.Error != null) {
                    // Handle errors
                } else {

                    string ip = e.Result as string;
                    callback?.Invoke(ip);

                }

            };

            worker.RunWorkerAsync();
        }

        /// <summary>
        /// Checks for 'evidence' of Docker running in WSL
        /// </summary>
        /// <returns>True if Docker is running; False if not.</returns>
        /// <remarks>Will start WSL if WSL isn't already running, which may or may not be desired.</remarks>
        public static bool IsRunning() {
            // TODO: LEFT OFF: don't think I'm returning this properly. RunCommand returns a string, and I'm not testing what it returns before finally returning a bool.
            bool bResult = false;

            Task task = Task.Run(() => {

                //Debug.WriteLine($"IsDockerRunning: Task started (ID: {Task.CurrentId})");

                bResult = Misc.RunCommand("wsl", "docker ps", string.Empty, true).Contains("CONTAINER ID");

                //Debug.WriteLine($"IsDockerRunning: Task complete (ID: {Task.CurrentId})");

            });

            task.Wait();
            return bResult;

        }

        public static void IsRunning_BGW(Action<bool> callback) {
            // TODO: LEFT OFF: Need to test and finish the regular one above first, then add any changes here as well.
            // Example usage:
            //
            // Docker.IsRunning_BGW((result) => {
            //     if (result) {
            //         lblWSLRunning.Text = "WSL: RUNNING";
            //         picWSL.Visible = true;
            //     } else {
            //         lblWSLRunning.Text = "WSL: N/A";
            //         picWSL.Visible = false;
            //     }
            // }, false);

            BackgroundWorker worker = new();

            worker.DoWork += (sender, e) => {

                Process[] p = Process.GetProcessesByName("wsl");
                bool isRunning = p.Length > 0;
                e.Result = isRunning;

            };

            worker.RunWorkerCompleted += (sender, e) => {

                if (e.Error != null) {
                    // Handle errors
                } else {

                    bool isRunning = (bool)e.Result;
                    callback?.Invoke(isRunning);

                }

            };

            worker.RunWorkerAsync();
        }

    }

    public static class Firewall {

        #region + -- NOTES  -- +

        // PowerShell Methods require NuGet Package: Microsoft.Powershell.SDK

        // Reference: https://learn.microsoft.com/en-us/windows/win32/api/netfw/nn-netfw-inetfwrule

        // - As of yet I haven't gone too deep into validation. Some things still need it to make sure the proper values get passed to the properties. i.e InterfaceTypes
        //   and https://learn.microsoft.com/en-us/windows/win32/api/netfw/nf-netfw-inetfwrule-get_localaddresses.
        // - Also, this (at least so far) isn't meant to handle all possible abilities of the Windows Firewall (not "all inclusive"); just some basic adding/removing.

        #endregion

        /// <summary>
        /// Adds a rule to the WIndows Firewall
        /// </summary>
        /// <param name="strLocalPorts">The string local ports.</param>
        /// <param name="strRemotePorts">The string remote ports.</param>
        /// <param name="strName">Name of the string.</param>
        /// <param name="strDescription">The string description.</param>
        /// <param name="strLocalAddresses">The string local addresses.</param>
        /// <param name="strRemoteAddresses">The string remote addresses.</param>
        /// <param name="bAllow">if set to <c>true</c> [b allow].</param>
        /// <param name="bDirectionOut">if set to <c>true</c> [b direction out].</param>
        /// <param name="bTCP">if set to <c>true</c> [b TCP].</param>
        /// <param name="bEnabled">if set to <c>true</c> [b enabled].</param>
        /// <param name="strInterfaceTypes">The string interface types.</param>
        /// <returns>0 on Successful Add; 1 on Failed Add</returns>
        public static int WinFirewall_Rule_Add(
            string strLocalPorts = "", 
            string strRemotePorts = "Any",
            string strName = "No Name Given",
            string strDescription = "No Description Given",  
            string strLocalAddresses = "*", 
            string strRemoteAddresses = "*", 
            bool bAllow = true, 
            bool bDirectionOut = true, 
            bool bTCP = true, 
            bool bEnabled = false, 
            string strInterfaceTypes = "All") {

            int intFailed = 0;

            try {

                // Create an instance of the Windows Firewall Manager
                INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

                // Create the new rule
                INetFwRule firewallRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));

                // Set properties for the new rule
                firewallRule.Action = bAllow ? NET_FW_ACTION_.NET_FW_ACTION_ALLOW : NET_FW_ACTION_.NET_FW_ACTION_BLOCK;                             // Allow or Block INCOMING traffic
                firewallRule.Description = strDescription.Replace("|", string.Empty).Trim();                                                        // Rule description ("|" not allowed)
                firewallRule.Direction = bDirectionOut ? NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT : NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;    // Traffic Direction
                firewallRule.Enabled = bEnabled;                                                                                                    // Enable/Disable the rule
                firewallRule.InterfaceTypes = strInterfaceTypes;                                                                                    // Interface (Choices are: "RemoteAccess", "Wireless", "Lan", & "All". Or any of them separated by commas)
                firewallRule.Name = strName.Replace("|", string.Empty).Replace("all", string.Empty).Trim();                                         // Rule name ("|" and "all" not allowed)
                firewallRule.Protocol = bTCP ? (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP : (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP;   // Protocol (you can also use 6 for TCP, or 17 for UDP).
                firewallRule.LocalPorts = strLocalPorts;                                                                                            // LOCAL port(s) to allow traffic. "RPC" is also acceptable.    TODO
                firewallRule.RemotePorts = strRemotePorts;                                                                                          // REMOTE port(s) to allow traffic.                             TODO
                firewallRule.LocalAddresses = strLocalAddresses;                                                                                    // ! Needs validation added later !
                firewallRule.RemoteAddresses = strRemoteAddresses;                                                                                  // ! Needs validation added later !

                //firewallRule.Profiles = firewallPolicy.CurrentProfileTypes;                                                                       // Note sure yet, look it up.                                   TODO

                // Add the rule to the Windows Firewall.
                firewallPolicy.Rules.Add(firewallRule);

            } catch (Exception ex) {

                intFailed = 1;
                Debug.WriteLine($"WinFirewall_RuleAdd(): {ex}");
                
            }

            return intFailed;

        }

        /// <summary>
        /// Delete a rule(s) from Windows Firewall
        /// </summary>
        /// <param name="strName">Name of the Rule to remove, as seen in the Firewall list.</param>
        /// <returns>0: Successful Deletion; Non-Zero upon failure.</returns>
        /// <remarks>When several names in the fw list match exactly, it deletes the first it finds.</remarks>
        /// <remarks>For best results, if you're in control of adding the rule(s) you're removing; add unique names. i.e. w/a random string or hash.</remarks>
        public static int WinFirewall_Rule_Remove(string strName) {

            int intFailed = 0;

            try {

                // Create an instance of the Windows Firewall Manager
                INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

                // Filter rules based on Name
                List<INetFwRule> matchingRules = firewallPolicy.Rules
                    .OfType<INetFwRule>()
                    .Where(x => x.Name == strName)
                    .ToList();

                // Check if any matching rule was found
                if (matchingRules.Count > 0) {

                    foreach (INetFwRule rule in matchingRules) {
                        firewallPolicy.Rules.Remove(rule.Name);
                    }

                } else {
                    intFailed = 1;
                }

            } catch (Exception ex) {
                intFailed = 1;
                Debug.WriteLine($"WinFirewall_RuleRemove(): {ex}");
            }
            return intFailed;
        }

        /// <summary>
        /// Delete a rule(s) from Windows Firewall. THis is just another way of doing the same as WinFirewall_Rule_Remove().
        /// </summary>
        /// <param name="strName">Name of the Rule to remove, as seen in the Firewall list.</param>
        /// <returns>0: Successful Deletion; Non-Zero upon failure.</returns>
        /// <remarks>When several names in the fw list match exactly, it deletes the first it finds.</remarks>
        /// <remarks>For best results, if you're in control of adding the rule(s) you're removing; add unique names. i.e. w/a random string or hash.</remarks>
        public static int WinFirewall_Rule_Remove2(string strName, string strLocalPorts, string strRemotePorts) {

            int intFailed = 0;

            try {

                // Create an instance of the Windows Firewall Manager
                INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

                // Get the list of rules
                List<INetFwRule> rules = firewallPolicy.Rules.OfType<INetFwRule>().ToList();

                // Find the exact rule(s) with matching name, local ports, and remote ports
                List<INetFwRule> matchingRules = rules.Where(rule => rule.LocalPorts == strLocalPorts && rule.RemotePorts == strRemotePorts).ToList();

                foreach (INetFwRule rule in matchingRules) {
                    firewallPolicy.Rules.Remove(rule.Name);
                }

                if (matchingRules.Count == 0) {
                    intFailed = 1;
                }

            } catch (Exception ex) {
                intFailed = 1;
                Debug.WriteLine($"WinFirewall_RuleRemove2(): {ex}");
            }

            return intFailed;
        }

        /// <summary>
        /// Adds a Firewall Rule using Powershell
        /// </summary>
        /// <remarks>UNFINISHED: See remark for the Remove Method</remarks>
        public static void WinFirewall_Rule_Add_PShell() {

            // Ref: https://stackoverflow.com/q/76857591/553663

            // TODO: (Eventually, if I feel like it). Pass these PowerShell commands to a command line.

            // Invoke-Expression "New-NetFireWallRule -DisplayName 'WSL2 Firewall Unlock' -Direction Outbound -LocalPort $ports_tcp -Action Allow -Protocol TCP -Group WSL -Description 'PortProxy for forwarding TCP ports to WSL'";
            // Invoke-Expression "New-NetFireWallRule -DisplayName 'WSL2 Firewall Unlock' -Direction Inbound -LocalPort $ports_tcp -Action Allow -Protocol TCP -Group WSL -Description 'PortProxy for forwarding TCP ports to WSL'";
            // Invoke-Expression "Remove-NetFireWallRule -DisplayName 'WSL2 Firewall Unlock'";

        }

        /// <summary>
        /// Removes a Firewall Rule using Powershell
        /// </summary>
        /// <param name="strName">Name of the Rule to remove</param>
        /// <returns>0 if successful; non-zero if failed.</returns>
        /// <remarks>UNFINISHED: Ran into issues with Remove-NetFireWallRule not recognized. Commented out for now so I can uninstall the unused Microsoft.Powershell.SDK Nuget Package bloat.</remarks>
        public static int WinFirewall_Rule_Remove_PShell(string strName) {

            // Ref: https://stackoverflow.com/q/76857591/553663

            int intFailed = 0;

            //// Create a new PowerShell instance
            //using (PowerShell ps = PowerShell.Create()) {

            //    ps.AddCommand("Set-ExecutionPolicy");
            //    ps.AddParameter("-ExecutionPolicy",  "Unrestricted");

            //    ps.AddScript("Import-Module NetSecurity");
            //    //ps.Invoke();

            //    // Add the Remove-NetFireWallRule cmdlet to the pipeline
            //    ps.AddCommand("Remove-NetFireWallRule");

            //    // Add the -DisplayName parameter with the rule name you want to remove
            //    ps.AddParameter("-DisplayName", strName);

            //    try {

            //        // Execute the PowerShell command
            //        Collection<PSObject> results = ps.Invoke();

            //        // Check for errors
            //        if (ps.HadErrors) {

            //            intFailed = 1;

            //            foreach (ErrorRecord error in ps.Streams.Error) {
            //                Debug.WriteLine($"WinFirewall_Rule_Remove_PShell(); PowerShell Error: {error}");
            //            }

            //        }

            //    } catch (Exception ex) {

            //        intFailed = 1;
            //        Debug.WriteLine($"WinFirewall_Rule_Remove_PShell(); Exception: {ex.Message}");
            //    }
            //}

            return intFailed;

        }

        /// <summary>
        /// Removes a Firewall Rule using Powershell via CommandLine
        /// </summary>
        /// <param name="strName">Name of the Rule to remove</param>
        /// <returns>0 if successful; non-zero if failed.</returns>
        /// <remarks>UNFINISHED: Remove-NetFireWallRule suddenly seems to no longer do anything, even in a Pshell terminal itself.</remarks>
        public static int WinFirewall_Rule_Remove_PShell_via_CMD(string strName) {
        
            int intFailed = 0;

            string strOutput = Misc.RunCommand("powershell.exe", $"Invoke-Expression \"Remove-NetFireWallRule -DisplayName '{strName}'\"");

            return intFailed;       
        
        }

    }

    public static class Hash {

        /// <summary>
        /// Generates/Computes an MD5 string hash
        /// </summary>
        /// <param name="strInput">String to use in the generation of the MD5 hash</param>
        /// <returns>An MD5 string hash</returns>
        public static string Generate_MD5(string strInput) {

            // Create an instance of the MD5 algorithm
            using (MD5 md5 = MD5.Create()) {

                // Convert the input string to bytes
                byte[] inputBytes = Encoding.UTF8.GetBytes(strInput);

                // Compute the MD5 hash
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the hash bytes to a hexadecimal string
                StringBuilder sb = new();

                for (int i = 0; i < hashBytes.Length; i++) {
                    sb.Append(hashBytes[i].ToString("x2"));
                }

                return sb.ToString();

            }

        }

        /// <summary>
        /// Compares 2 MD5 strings and returns whether they match or not
        /// </summary>
        /// <param name="strFirstHash">1st MD5 string to compare</param>
        /// <param name="strSecondHash">2nd MD5 string to compare to the 1st</param>
        /// <returns>True on matching MD5 string; False otherwise.</returns>
        /// <remarks>Use the Generate_MD5 method above first, to get the MD5 strings to compare.</remarks>
        public static bool IsMatch_MD5(string strFirstHash, string strSecondHash) {
            return (strFirstHash.Equals(strSecondHash));
        }

    }

    public static partial class IP_Regex {

        #region + -- REGEX -- +

        // IP Detections
        public static Regex IPv4RegEx = IPv4Pattern();
        public static Regex IPv6RegEx = IPv6Pattern();

        // Port Detections
        public static Regex IPv4PortRegEx = IPv4PortPattern();
        public static Regex IPv6PortRegEx = IPv6PortPattern();


        //[GeneratedRegex("\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}")]
        //[GeneratedRegex("(\\d{1,3}\\.){3}\\d{1,3}")]
        [GeneratedRegex(@"(\d{1,3}\.){3}\d{1,3}")]
        private static partial Regex IPv4Pattern();

        // NOTE: Re-test this. Not sure it works, due to the ^ and $
        [GeneratedRegex("^(([0-9a-fA-F]{1,4}:){7}([0-9a-fA-F]{1,4}|:))|(([0-9a-fA-F]{1,4}:){0,6}(:[0-9a-fA-F]{1,4}){1,6})$")]
        private static partial Regex IPv6Pattern();

        [GeneratedRegex(@"((\d{1,3}\.){3}\d{1,3}):(\d+)")]
        private static partial Regex IPv4PortPattern();
        
        // NOTE: This is specific to the output from the netstat cmd
        [GeneratedRegex(@"tcp6\s+\d+\s+\d+\s+(::\d?):(\d+|:\d+)\s+.*")]
        private static partial Regex IPv6PortPattern();

        #endregion

    }

    public static class Listview {

        /// <summary>
        /// Returns number of items in a listview group
        /// </summary>
        /// <param name="listView">A ListView object</param>
        /// <param name="group">The Group to count items in</param>
        /// <returns>Number if items in that group</returns>
        public static int CountItemsInGroup(ListView listView, ListViewGroup group) {

            int count = 0;

            foreach (ListViewItem item in listView.Items) {

                if (item.Group == group) {
                    count++;
                }

            }
            return count;
        }

        /// <summary>
        /// Returns number of items in a listview group based on the group header (similar to group name)
        /// </summary>
        /// <param name="listView">A ListView object</param>
        /// <param name="strHeader">The Group Header to count items in</param>
        /// <returns>Number if items in that group</returns>
        public static int CountItemsInGroup(ListView listView, string strHeader) {
            // TODO: add Default group handling as well; also add example usage comment.
            ListViewGroup group = null;
            int itemCount = 0;

            foreach (ListViewGroup listViewGroup in listView.Groups) {

                // NOTE: This entire method could easily be modified to match something else by changing the following line; i.e. listViewGroup.Name
                if (listViewGroup.Header == strHeader) {

                    group = listViewGroup;
                    break;

                }

            }

            if (group != null) {
                itemCount = group.Items.Count;
            } else {
                // The group with the specified name was not found
            }
            return itemCount;
        }

    }

    public static class ListBoxes {

        /// <summary>
        /// Sorts a ListBox Numerically, rather than the default Albhabetically.
        /// </summary>
        /// <param name="listBox">A ListBox object</param>
        public static void SortItemsNumerically(ListBox listBox) {

            // Example:
            // ListBoxes.SortItemsNumerically(listBox1);

            // Make sure built-in sorting is off, or it'll override our work here:
            listBox.Sorted = false;

            // Step 1: Get the list of items from the ListBox
            List<string> items = listBox.Items.Cast<string>().ToList();

            // Step 2: Convert the items to a numeric type (e.g., int or double)
            List<int> numericItems = items.Select(item => {
                if (int.TryParse(item, out int numericValue)) {
                    return numericValue;
                } else {
                    // Handle the case where an item is not a valid numeric value
                    return 0; // or any default value
                }
            }).ToList();

            // Step 3: Sort the items using the numeric values
            numericItems.Sort();

            // Step 4: Clear the ListBox
            listBox.Items.Clear();

            // Step 5: Add the sorted items back to the ListBox
            listBox.Items.AddRange(numericItems.Select(numericItem => numericItem.ToString()).ToArray());

        }

    }

    public static partial class Network {

        #region + -- DECLARATIONS -- +

        private static readonly string strLink_PortsTest = "https://www.yougetsignal.com/tools/open-ports";

        #endregion

        public static partial class DNS {

            [return: MarshalAs(UnmanagedType.Bool)]
            [LibraryImport("dnsapi.dll")]
            internal static partial bool DnsFlushResolverCache();

            /// <summary>
            /// Flushes the system's DNS cache
            /// </summary>
            /// <param name="bConfirm">[optional: default true] Show a confirmation MessageBox first</param>
            /// <param name="bResult">[optional: default true] Show a 'success' confirmation MessageBox when done</param>
            /// <exception cref="InvalidOperationException"></exception>
            /// <returns>True on Successful flush, False or Exception on Failure</returns>
            public static bool FlushCache(bool bConfirm = true, bool bResult = true) {

                // Show dialog asking for Flush confirmation, giving them a chance to bail out if misclicked.
                if (bConfirm && MessageBox.Show("Flush DNS?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No) {

                    Debug.WriteLine("FlushCache(): User chose to not flush cache; exiting method.");
                    return false;
                }

                // If use either clicked Yes yes above, or bConfirm was False, flush.
                bool status = DnsFlushResolverCache();

                if (!status) {
                    throw new InvalidOperationException("FlushCache(): FlushDNS Cache failed.");
                } else {

                    // Show a confirmation dialog if desired
                    if (bResult) {
                        MessageBox.Show("DNS Flushed!", "Whoosh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return true;
                }

            }

        }

        /// <summary>
        /// Check if an IPv4 IP is connectable on a specified port
        /// </summary>
        /// <param name="strHost">hostname or IP</param>
        /// <param name="intPort">[optional: default 80] Port to test</param>
        /// <returns>True = Connected; Else = Can't Connect</returns>
        public static bool CheckPortOpen(string strHost, int intPort = 80) {

            bool bIPv6 = false;

            if (IsIPv6(strHost)) {

                bIPv6 = true;

            } else if (IsIPv4(strHost)) {

                bIPv6 = false;

            } else {

                // Not a valid IP; exit function
                Debug.WriteLine($"CheckPortOpen(): Invalid IP {strHost}");
                return false;

            }

            // + --- +


            // Create an endpoint with the IPv6 address and port
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(strHost), intPort);

            Socket sock = new Socket(bIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Set the socket options to use IPv6 and allow reusing the same port, and also allow receiving IPv4 traffic on the same port
            sock.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, 0);
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Attempt to connect
            try {

                Debug.WriteLine($"TESTING: IP{(bIPv6 ? "6" : "4")}:Port {strHost}:{intPort}");
                sock.Connect(endpoint);

                // If success, these get called, else, passed over for SocketException below.
                Debug.WriteLine($"TESTING (SUCCESS): IP{(bIPv6 ? "6" : "4")} Port {intPort} is open on {strHost}.");
                return true;

            } catch (SocketException sx) {
                Debug.WriteLine($"TESTING (FAILED): IP{(bIPv6 ? "6" : "4")} Port {intPort} is closed on {strHost}. (Code: {sx.ErrorCode.ToString()})");
            } finally {
                sock.Close();
            }

            return false;
        }

        /// <summary>
        /// Retrieves the current machine's local IP(s)
        /// </summary>
        /// <param name="bIPv6">[optional: default false] True to return the IP6 IP, False for IP4 IP.</param>
        /// <returns>Local IP(s) as a List of Strings</returns>
        /// <exception cref="Exception"></exception>
        public static List<string> GetLocalIPAddress(bool bIPv6 = false) {
            // TODO: IPv6 portion unfinished

            // Usage Example:
            //
            // List<string> lstIPs = Network.GetLocalIPAddress();

            List<string> lstIP = new();

            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress ip in host.AddressList) {

                if (ip.AddressFamily == AddressFamily.InterNetworkV6 && bIPv6 || ip.AddressFamily == AddressFamily.InterNetwork && !bIPv6) {
                    lstIP.Add(ip.ToString());
                } else {
                    //throw new Exception("No network adapters with an IPv4 address in the system!");
                }

            }
 
            return lstIP;

        }

        /// <summary>
        /// Gets all NIC and their current connectivity status
        /// </summary>
        /// <returns>Dictionary <"NIC Name", "Status"></returns>
        public static Dictionary<string, string> GetNICStatus() {

            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            Dictionary<string, string> dicStatus = new();

            foreach (NetworkInterface networkInterface in networkInterfaces) {
                dicStatus.Add(networkInterface.Name, networkInterface.OperationalStatus.ToString());
            }

            return dicStatus;

        }

        /// <summary>
        /// Regex Validates IPv4 string
        /// </summary>
        /// <param name="ip"><string>IP Address to check</string></param>
        /// <returns></returns>
        public static bool IsIPv4(string ip) {
            return ip.IsMatch(IP_Regex.IPv4RegEx);
        }

        /// <summary>
        /// Validates a potential IPv6 Address
        /// </summary>
        /// <param name="ip">IP to validate</param>
        /// <returns>True if valid; False if Invalid</returns>
        public static bool IsIPv6(string ip) {

            // Scott Note to original author: Is this correct? Looks more like a MAC Address regex? I added a better regex below, but leaving original for posterity.
            // return ip.IsMatch(new Regex(@"^[\dABCDEF]{2}(?::(?:[\dABCDEF]{2})){5}$"));
            return ip.IsMatch(IP_Regex.IPv6RegEx);

        }

        public static bool IsNetworkAvailable() {
            return NetworkInterface.GetIsNetworkAvailable();
        }

        /// <summary>
        /// Open a website in the browser to test your ports
        /// </summary>
        public static void Link_OpenPortTester() {
            Misc.RunCommand(strLink_PortsTest);
        }
    
    }

    public static class Misc {

        /// <summary>
        /// Standard Process Start function
        /// </summary>
        /// <param name="strFileName">Filename|Program.exe to run</param>
        /// <param name="strArgs">[optional] Any arguments for the above file|program</param>
        /// <returns>Program output if any, else empty string</returns>
        public static string RunCommand(string strFileName, string strArgs = "", string strWorkingDirectory = "", bool bGetOutput = true) {

            string strOutput = string.Empty;
            string pattern = @"(http|https|ftp)://";

            // Determine if a URL has been passed. Using this Process method requires a lil bit of special handling in such case.
            MatchCollection matches = Regex.Matches(strFileName, pattern);

            if (matches.Count > 0) {

                // Switch the filename (which is a URL) for strArgs, and append any specifically added Args after that.
                strArgs = $"{strFileName} {strArgs}";

                // Use Explorer by default
                strFileName = "explorer.exe";

            }

            try {

                using Process p = new();
                {
                    ProcessStartInfo withBlock = p.StartInfo;
                    withBlock.Verb = "runas";
                    withBlock.RedirectStandardOutput = true;
                    withBlock.RedirectStandardError = true;
                    withBlock.FileName = strFileName.Trim();
                    withBlock.Arguments = strArgs.Trim();
                    withBlock.UseShellExecute = false;
                    withBlock.CreateNoWindow = true;
                    withBlock.WorkingDirectory = strWorkingDirectory.Trim();
                }

                p.Start();

                // If we always do the following, the app will lock up when doing something like starting WSL, so it's optional.
                if (bGetOutput) {

                    // Remove any null chars, if exist.
                    strOutput = p.StandardOutput.ReadToEnd().Replace("\0", "");
                    p.WaitForExit();

                }

            } catch (Exception e) {
                Debug.WriteLine($"RunCommand() Error: {e.Message}");
                throw;
            }

            return strOutput;
        }

        /// <summary>
        /// Standard Process Start function (async: Task)
        /// </summary>
        /// <param name="strFileName">Filename|Program.exe to run</param>
        /// <param name="strArgs">[optional] Any arguments for the above file|program</param>
        /// <returns>Program output if any, else empty string</returns>
        public static async Task<string> RunCommandAsync(string strFileName, string strArgs = "", string strWorkingDirectory = "", bool bGetOutput = true) {

            string strOutput = string.Empty;
            string pattern = @"(http|https|ftp)://";

            // Determine if a URL has been passed. Using this Process method requires a lil bit of special handling in such case.
            MatchCollection matches = Regex.Matches(strFileName, pattern);

            if (matches.Count > 0) {

                // Switch the filename (which is a URL) for strArgs, and append any specifically added Args after that.
                strArgs = $"{strFileName} {strArgs}";

                // Use Explorer by default
                strFileName = "explorer.exe";

            }

            try {

                using Process p = new Process();
                {
                    ProcessStartInfo withBlock = p.StartInfo;
                    withBlock.Verb = "runas";
                    withBlock.RedirectStandardOutput = true;
                    withBlock.RedirectStandardError = true;
                    withBlock.FileName = strFileName.Trim();
                    withBlock.Arguments = strArgs.Trim();
                    withBlock.UseShellExecute = false;
                    withBlock.CreateNoWindow = true;
                    withBlock.WorkingDirectory = strWorkingDirectory.Trim();
                }

                p.Start();

                if (bGetOutput) {

                    // Remove any null chars, if exist.
                    strOutput = await p.StandardOutput.ReadToEndAsync();
                    await p.WaitForExitAsync();
                }

            } catch (Exception e) {

                Debug.WriteLine($"RunCommand(): {e.Message}");
                throw;

            }

            return strOutput;
        }

        /// <summary>
        /// Checks if a process is running
        /// </summary>
        /// <param name="strProcessName">Name of the process</param>
        /// <returns>True: running; False: not running.</returns>
        public static bool IsProcessRunning(string strProcessName) {

            Process[] p = Process.GetProcessesByName(strProcessName);
            return p.Any();

        }

    }

    public static class Services {

        #region + -- WIN32API DECLARATIONS -- +

        internal enum GenericRights : uint {
            GENERIC_READ = 0x80000000,
            GENERIC_WRITE = 0x40000000,
            GENERIC_EXECUTE = 0x20000000,
            GENERIC_ALL = 0x10000000,
        }

        internal static class NativeMethods {

            [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess);

            [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", SetLastError = true, CharSet = CharSet.Unicode)]
            internal static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, ServiceRights dwDesiredAccess);

            [DllImport("advapi32.dll", EntryPoint = "QueryServiceStatus", CharSet = CharSet.Auto)]
            internal static extern bool QueryServiceStatus(IntPtr hService, ref ServiceStatus dwServiceStatus);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool ControlService(IntPtr hService, ServiceControls dwControl, ref ServiceStatus lpServiceStatus);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseServiceHandle(IntPtr hSCObject);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool StartService(IntPtr hService, int dwNumServiceArgs, string[] lpServiceArgVectors);

        }

        internal enum ScmRights : uint {
            SC_MANAGER_CONNECT = 0x0001,
            SC_MANAGER_CREATE_SERVICE = 0x0002,
            SC_MANAGER_ENUMERATE_SERVICE = 0x0004,
            SC_MANAGER_LOCK = 0x0008,
            SC_MANAGER_QUERY_LOCK_STATUS = 0x0010,
            SC_MANAGER_MODIFY_BOOT_CONFIG = 0x0020,

            SC_MANAGER_ALL_ACCESS =
                StandardRights.STANDARD_RIGHTS_REQUIRED
                | SC_MANAGER_CONNECT
                | SC_MANAGER_CREATE_SERVICE
                | SC_MANAGER_ENUMERATE_SERVICE
                | SC_MANAGER_LOCK
                | SC_MANAGER_QUERY_LOCK_STATUS
                | SC_MANAGER_MODIFY_BOOT_CONFIG
        }

        //[Flags]
        internal enum ServiceControls : uint {
            SERVICE_CONTROL_PARAMCHANGE = 0x00000006,
        }

        [Flags]
        internal enum ServiceRights : uint {
            SERVICE_QUERY_CONFIG = 0x0001,
            SERVICE_CHANGE_CONFIG = 0x0002,
            SERVICE_QUERY_STATUS = 0x0004,
            SERVICE_ENUMERATE_DEPENDENTS = 0x0008,
            SERVICE_START = 0x0010,
            SERVICE_STOP = 0x0020,
            SERVICE_PAUSE_CONTINUE = 0x0040,
            SERVICE_INTERROGATE = 0x0080,
            SERVICE_USER_DEFINED_CONTROL = 0x0100,

            SERVICE_ALL_ACCESS =
                SERVICE_QUERY_CONFIG
                | SERVICE_CHANGE_CONFIG
                | SERVICE_QUERY_STATUS
                | SERVICE_ENUMERATE_DEPENDENTS
                | SERVICE_START
                | SERVICE_STOP
                | SERVICE_PAUSE_CONTINUE
                | SERVICE_INTERROGATE
                | SERVICE_USER_DEFINED_CONTROL
        }

        internal enum ServiceState {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ServiceStatus {
            public uint dwServiceType;
            public ServiceState dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
        }

        internal enum StandardRights : uint {
            STANDARD_RIGHTS_REQUIRED = 0x000F0000,
        }

        #endregion

        /// <summary>
        /// Gets information abot a Windows Service. Result in Debug window.
        /// </summary>
        /// <param name="serviceName">Name of the service</param>
        /// <exception cref="InvalidOperationException">
        /// Service '{serviceName}' is not in a running state.
        /// or
        /// Failed to pause and continue service '{serviceName}'.
        /// </exception>
        /// <returns>Service Information in a String</returns>
        public static string GetInfo(string serviceName) {

            // Ref: https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-service

            // Usage Example: Debug.WriteLine(Services.GetInfo("iphlpsvc"));

            StringBuilder sbResult = new();

            using (ServiceController serviceController = new ServiceController(serviceName)) {

                if (serviceController.Status != ServiceControllerStatus.Running) {
                    throw new InvalidOperationException($"Service '{serviceName}' is not in a running state.");
                }

                try {

                    using (ManagementObject service = new($"Win32_Service.Name='{serviceController.ServiceName}'")) {
                        
                        service.Get();

                        sbResult.AppendLine("");
                        sbResult.AppendLine("+ -------------------------------------------------- +");
                        sbResult.AppendLine($"| SERVICE INFO FOR: {serviceName}");
                        sbResult.AppendLine("+ -------------------------------------------------- +");
                        sbResult.AppendLine($"| Display Name:     {serviceController.DisplayName}");
                        sbResult.AppendLine($"| Service Name:     {serviceController.ServiceName}");
                        sbResult.AppendLine($"| Status:           {serviceController.Status}");
                        sbResult.AppendLine("+ -------------------------------------------------- +");
                        sbResult.AppendLine($"| ABILITIES:");
                        sbResult.AppendLine("+ -------------------------------------------------- +");
                        sbResult.AppendLine($"| Stop?             {serviceController.CanStop}");
                        sbResult.AppendLine($"| ShutDown?         {serviceController.CanShutdown}");
                        sbResult.AppendLine($"| Pause & Continue? {serviceController.CanPauseAndContinue}");
                        sbResult.AppendLine("+ -------------------------------------------------- +");
                        sbResult.AppendLine($"| EXTRA:");
                        sbResult.AppendLine("+ -------------------------------------------------- +");
                        sbResult.AppendLine($"| CMD:   {service["PathName"]}");
                        sbResult.AppendLine($"| Owner: {service["StartName"]}");
                        sbResult.AppendLine($"| Desc.: {service["Description"]}");
                        sbResult.AppendLine("+ -------------------------------------------------- +");
                        sbResult.Append("");

                    }

                } catch (InvalidOperationException ex) {
                    throw new InvalidOperationException($"Failed to pause and continue service '{serviceName}'.", ex);
                }
            }
            return sbResult.ToString(); 
        }

        /// <summary>
        /// Determines whether a Windows Service is running. (async)
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <param name="strServiceName">Exact name of the service (case-sensitive)</param>
        /// <param name="bShowMessage">[optional: default: False] If True, show the result in a MessageBox as well.</param>
        public static void IsRunning_BGW(Action<bool> callback, string strServiceName, bool bShowMessage = false) {

            // Original PPG added it's own 'IsRunning' method that uses WinAPI, but I saw no need to include it along w/this one.
            // https://github.com/zmjack/PortProxyGUI/blob/fe775680217f5aac647217d4ee4f47abbaeb6aa3/PortProxyGUI/Utils/PortPorxyUtil.cs#L90C17-L90C17

            // Example usage:
            //
            // Services.IsRunning_BGW((result) => {
            //
            //     if (result) {
            //         Do something if True
            //     } else {
            //         Do something if False
            //     }
            //
            // }, "iphlpsvc", false);

            BackgroundWorker worker = new();

            worker.DoWork += (sender, e) => {

                bool isRunning = false;

                ServiceController service = new(strServiceName);

                try {

                    // Check the status of the service
                    isRunning = service.Status == ServiceControllerStatus.Running;

                } catch (Exception) {

                    // The service does not exist or an error occurred
                   isRunning = false;

                }
                
                e.Result = isRunning;

            };

            worker.RunWorkerCompleted += (sender, e) => {

                if (e.Error != null) {
                    // Handle errors
                } else {

                    bool isRunning = (bool)e.Result;

                    if (bShowMessage) {
                        MessageBox.Show($"{strServiceName} {(isRunning ? "is" : "is not")} running. ", "Service", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    callback?.Invoke(isRunning);

                }

            };
            worker.RunWorkerAsync();
        }

        /// <summary>
        /// Changes/Reloads a Service's Startup Parameters
        /// </summary>
        /// <param name="serviceName">Name of the service</param>
        /// <exception cref="InvalidOperationException">
        /// SC Manager Open: failed.
        /// or
        /// Service Open: ({serviceName}) failed.
        /// or
        /// Service Control: ({serviceName}) ParamChange failed.
        /// </exception>
        /// <returns>True: Success; False or Exception on Failure</returns>
        public static bool ParamChangeWinAPI(string serviceName) {

            bool bResult = true;

            IntPtr hManager = NativeMethods.OpenSCManager(null, null, (uint)GenericRights.GENERIC_READ | (uint)ScmRights.SC_MANAGER_CONNECT);

            if (hManager == IntPtr.Zero) throw new InvalidOperationException("SC Manager Open: failed.");

            IntPtr hService = NativeMethods.OpenService(hManager, serviceName, ServiceRights.SERVICE_PAUSE_CONTINUE);

            if (hService == IntPtr.Zero) {
                NativeMethods.CloseServiceHandle(hManager);
                throw new InvalidOperationException($"Service Open: ({serviceName}) failed.");
            }

            ServiceStatus serviceStatus = new();
            bool success = NativeMethods.ControlService(hService, ServiceControls.SERVICE_CONTROL_PARAMCHANGE, ref serviceStatus);

            NativeMethods.CloseServiceHandle(hService);
            NativeMethods.CloseServiceHandle(hManager);

            if (!success) {
                Debug.WriteLine($"ParamChangeWinAPI(): ({serviceName}) ParamChange failed.");
                bResult = false;
            }

            return bResult;
        }

        public static void ParamChange(string serviceName) {

            using (ServiceController serviceController = new(serviceName)) {

                if (serviceController.Status != ServiceControllerStatus.Running) {
                    throw new InvalidOperationException($"Service '{serviceName}' is not in a running state.");
                }

                try {

                    //serviceController.Pause();
                    //serviceController.Continue();
                    serviceController.Refresh();
                    // LEFT OFF: Not sure if Refresh is doing the same as the previous API.
                    } catch (InvalidOperationException ex) {
                    throw new InvalidOperationException($"Failed to pause and continue service '{serviceName}'.", ex);
                }

            }
        }

        /// <summary>
        /// Starts a Windows Service (async: BackgroundWorker)
        /// </summary>
        /// <param name="callback">The Result of the Start attempt</param>
        /// <param name="strServiceName">Name of the Service to start</param>
        public static void Start_BGW(Action<string> callback, string strServiceName) {

            // Original PPG added it's own Start Service method that uses WinAPI, but I saw no need to include it along w/this one.
            // https://github.com/zmjack/PortProxyGUI/blob/fe775680217f5aac647217d4ee4f47abbaeb6aa3/PortProxyGUI/Utils/PortPorxyUtil.cs#L111C42-L111C42

            // Example usage:
            //
            // Services.Start_BGW((result) => {
            //     Debug.WriteLine(result);
            // }, "iphlpsrv");

            BackgroundWorker worker = new();

            worker.DoWork += (sender, e) => {

                using (ServiceController serviceController = new(strServiceName)) {

                    try {

                        if (serviceController.Status != ServiceControllerStatus.Running) {
                            serviceController.Start();
                            serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                        }

                        e.Result = $"{strServiceName} Service started";

                    } catch (InvalidOperationException ex) {

                        e.Result = $"{strServiceName} Service failed to start: {ex.Message}";

                    } catch (System.ServiceProcess.TimeoutException ex) {

                        e.Result = $"{strServiceName} Service timed out while waiting to start: {ex.Message}";

                    } catch (Exception ex) {

                        e.Result = $"{strServiceName} error occurred while starting Service: {ex.Message}";

                    }
                }

            };

            worker.RunWorkerCompleted += (sender, e) => {

                string strResult = string.Empty;

                if (e.Error != null) {
                    // Handle any errors that occurred during service start
                    strResult = $"{strServiceName} Service Error: {e.Error.Message}";
                } else if (e.Cancelled) {
                    // Handle cancellation if needed
                    strResult = $"{strServiceName} Service start canceled";
                } else if (e.Result != null) {
                    // Handle any results returned from the background operation
                    strResult = $"{strServiceName} {e.Result}";
                } else {
                    // Service start completed successfully
                    strResult = $"{strServiceName} Service started";
                }

                Debug.WriteLine(strResult);
                callback?.Invoke(e.Result.ToString());

            };
            worker.RunWorkerAsync();
        }

    }

    public static class Strings {

        /// <summary>
        /// Converts to line endings bi-directionally between Windows & Unix.
        /// </summary>
        /// <param name="strInput">String to convert</param>
        /// <param name="toUnixLineEndings"><c>true</c> [to unix line endings]</param>
        /// <returns>Original string with converted line endings of your choice</returns>
        public static string ConvertLineEndings(string strInput, bool toUnixLineEndings = false) {

            // Dammit. I wrote this before I realized there's already a built-in .ReplaceLineEndings() function in .NET; leaving here for posterity.

            // Convert line endings to Windows format (CRLF) by default
            string targetLineEnding = "\r\n";

            if (toUnixLineEndings) {

                //Debug.WriteLine("Converting line endings to Unix format (LF)");
                targetLineEnding = "\n";

            }

            return strInput.Replace("\r\n", "\n").Replace("\r", "").Replace("\n", targetLineEnding);

        }

        /// <summary>
        /// Generates a random string of letters and numbers.
        /// </summary>
        /// <param name="length">Length of string wanted</param>
        /// <returns>A random string the length you wanted. Useful for very simple 'hashes' to make something unique.</returns>
        public static string GenerateRandomString(int length) {

            const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            
            StringBuilder result = new(length);
            Random random = new();

            for (int i = 0; i < length; i++) {

                int index = random.Next(characters.Length);
                result.Append(characters[index]);

            }

            return result.ToString();
        }

    }

    public static class Winsock {

            /// <summary>
            /// Resets the Winsock
            /// </summary>
            public static void Reset() {

                string strInfo = string.Format(
                    "{0}{3}{3}{1}{3}{3}{2}",
                    "This command also resets other networking components like the TCP/IP stack, which can fix various network issues like connectivity problems, DNS resolution issues, and more.",
                    "Note that resetting the Winsock should be used as a last resort to fix network issues, as it can affect any running network applications and may cause other issues. If you are experiencing network issues, it may be better to diagnose and fix the root cause of the problem rather than resetting the Winsock.",
                    "After running the netsh winsock reset command, you will need to restart your computer for the changes to take effect.",
                    Environment.NewLine
                );

                if (Dialogs.CustomDialog(strInfo, "Winsock Reset") == DialogResult.OK) {

                    // Run the winsock reset command
                    string strOutput = Misc.RunCommand("netsh.exe", "winsock reset");

                    // Report back the result of the reset attempt
                    if (strOutput.ToLower().Contains("sucessfully reset")) {

                        MessageBox.Show("You must restart the computer in order to complete the reset.", "Sucessfully reset your Winsock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                    } else {

                        MessageBox.Show(strOutput, "Couldn't reset your Winsock", MessageBoxButtons.OK, MessageBoxIcon.Error);
                
                    }
                }
            }
        }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "I want it all caps")]
    public static class WSL {

        public static void GetInfo_BGW(Action<OrderedDictionary> callback) {

            // Example usages:
            //
            // Version Only:
            // WSL.GetInfo_BGW((WSLVersion) => Debug.WriteLine($"WSL{(!string.IsNullOrEmpty(WSLVersion) ? " (v" + (WSLVersion) + ")" : string.Empty)}: RUNNING"), true);
            //
            // Full:
            // WSL.GetInfo_BGW((WSLInfo) => Debug.WriteLine($"{(WSLInfo)}"));
            //
            // Another:
            //
            // Version Only:
            // WSL.GetInfo_BGW((WSLVersion) => {
            //     Debug.WriteLine($"WSL Version: {WSLVersion}");
            // }, true);

            BackgroundWorker worker = new BackgroundWorker();

            worker.DoWork += (sender, e) => {

                // Fetch WSL info
                string strInput = Misc.RunCommand("wsl", $@"uptime | sed -E -e 's/(.* up|[^,]*: )//g' -e 's/, +/,/g' -e 's/(([0-9]+ days),)?([^,]*),/\2 \3,/' -e 's/^ +//' -e 's/([^,]+),([^,]+),([^,]+)$/\1 \2 \3/'").Trim();

                // Split the output into an array using comma as the delimiter
                string[] infoArray = strInput.Split(',');

                // Validate proper input string. Must have either 3 or 4 elements to proceed.
                if (infoArray.Length != 3 && infoArray.Length != 4) {
                    e.Result = null;
                    return;
                }

                // Stores final results
                OrderedDictionary dicInfo = new();

                // UPTIME: Add proper uptime based on how many elements we pulled
                dicInfo.Add("Uptime:", infoArray.Length == 4 ? $"{infoArray[0]}, {infoArray[1]}" : infoArray[0]);

                // USERS: Add User count. It'll always be the 2nd to last element.
                dicInfo.Add("Users:", infoArray[^2]);

                // LOAD AVERAGE: Fish out the Load Average for prettifying. It'll always be the last element, se we can just do it this way.
                string loadAvg = infoArray[^1];

                // Chop it up
                string[] loadArray = loadAvg.Split(' ');

                // Add em raw
                dicInfo.Add("Load Average:", loadAvg);
                dicInfo.Add("Load Average: 1m", $"{loadArray[0]}");
                dicInfo.Add("Load Average: 5m", $"{loadArray[1]}");
                dicInfo.Add("Load Average: 15m", $"{loadArray[2]}");

                loadArray[0] = $"1m ({loadArray[0]})";
                loadArray[1] = $"5m ({loadArray[1]})";
                loadArray[2] = $"15m ({loadArray[2]})";

                // Add some lipstick
                dicInfo.Add("Load Average: Pretty", $"{loadArray[0]} {loadArray[1]} {loadArray[2]}");
                dicInfo.Add("Load Average: 1m Pretty", $"{loadArray[0]}");
                dicInfo.Add("Load Average: 5m Pretty", $"{loadArray[1]}");
                dicInfo.Add("Load Average: 15m Pretty", $"{loadArray[2]}");

                // UPTIME SINCE: Let's juice it up and add some more stuff; fetch the info
                strInput = Misc.RunCommand("wsl", $@"uptime -s").Trim();

                // Add Up Since to the very first element
                dicInfo.Insert(0, "Up Since:", DateTime.TryParse(strInput, out DateTime result) ? strInput : string.Empty);

                // UPTIME SINCE (PRETTY): Let's juice it up and add some more stuff; fetch the info
                strInput = Misc.RunCommand("wsl", $@"uptime -p").Trim();

                // Add to the 2nd element
                dicInfo.Insert(1, "Up Since: Pretty", strInput.Contains("up") ? strInput.Replace("up ", string.Empty) : string.Empty);

                // For debugging
                //foreach (DictionaryEntry de in dicInfo) {
                //    Debug.WriteLine($"{de.Key} {de.Value}");
                //}

                e.Result = dicInfo.Count > 0 ? dicInfo : null;

            };

            worker.RunWorkerCompleted += (sender, e) => {

                if (e.Error != null) {
                    // Handle errors
                } else {

                    OrderedDictionary dicInfo = e.Result as OrderedDictionary;

                    if (dicInfo != null) {
                        callback?.Invoke(dicInfo);
                    }

                }

            };

            worker.RunWorkerAsync();
        }

        /// <summary>
        /// Checks if WSL is running
        /// </summary>
        /// <param name="bShowMessage">[optional: default False] will show a messagebox with the result.</param>
        /// <returns>True: WSL is running; False: WSL isn't running.</returns>
        public static bool IsRunning(bool bShowMessage = false) {

            bool bResult;

            //Debug.WriteLine($"WSL.IsRunning: Task started (ID: {Task.CurrentId})");
            bResult = Misc.IsProcessRunning("wsl");
            //Debug.WriteLine($"WSL.IsRunning: Task complete (ID: {Task.CurrentId})");

            if (bShowMessage) {
                MessageBox.Show($"WSL {(bResult ? "is" : "is not")} running.", "WSL: Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return bResult;

        }

        /// <summary>
        /// Checks if WSL is running (async: BackgroundWorker)
        /// </summary>
        /// <param name="bShowMessage">[optional: default False] will show a messagebox with the result.</param>
        /// <returns>True: WSL is running; False: WSL isn't running</returns>
        public static void IsRunning_BGW(Action<bool> callback, bool bShowMessage = false) {

            // Example usage:
            //
            // WSL.IsRunning_BGW((result) => {
            //     if (result) {
            //         lblWSLRunning.Text = "WSL: RUNNING";
            //         picWSL.Visible = true;
            //     } else {
            //         lblWSLRunning.Text = "WSL: N/A";
            //         picWSL.Visible = false;
            //     }
            // }, false);

            BackgroundWorker worker = new();

            worker.DoWork += (sender, e) => {

                Process[] p = Process.GetProcessesByName("wsl");
                bool isRunning = p.Length > 0;
                e.Result = isRunning;

            };

            worker.RunWorkerCompleted += (sender, e) => {

                if (e.Error != null) {
                    // Handle errors
                } else {

                    bool isRunning = (bool)e.Result;

                    if (bShowMessage) {
                        MessageBox.Show($"WSL {(isRunning ? "is" : "is not")} running.", "WSL: Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    callback?.Invoke(isRunning);

                }

            };

            worker.RunWorkerAsync();
        }

        /// <summary>
        /// Gets a list of all distros installed, their WSL version, and running state. (async: Task)
        /// </summary>
        /// <returns>Success: WSL Distro info; Fail: Empty string.</returns>
        public static string GetDistros() {

            string strOutput = string.Empty;

            Task task = Task.Run(() => {

                Debug.WriteLine($"WSL.GetDistros(): Task started (ID: {Task.CurrentId})");

               // Run the command
               strOutput = Misc.RunCommand("wsl.exe", "-l -v --all");

                Debug.WriteLine($"WSL.GetDistros(): Task complete (ID: {Task.CurrentId})");

            });

            task.Wait();

            // TODO: Should be beefed up with validation (make sure proper info was fetched)
            return strOutput.Length > 0 ? strOutput : string.Empty;
        }

        /// <summary>
        /// Gets *all* version info of the currently installed WSL version (async: Task)
        /// </summary>
        /// <returns>Success: WSL Version info; Fail: Empty string.</returns>
        public static string GetAllVersionInfo() {

            string strOutput = string.Empty;

            Task task = Task.Run(() => {

                Debug.WriteLine($"WSL.GetAllVersionInfo(): Task started (ID: {Task.CurrentId})");

                // Run the command
                strOutput = Misc.RunCommand("wsl.exe", "--version");

                Debug.WriteLine($"WSL.GetAllVersionInfo(): Task complete (ID: {Task.CurrentId})");

            });

            task.Wait();

            // TODO: Should be beefed up with validation (make sure proper info was fetched)
            return strOutput.Length > 0 ? strOutput : string.Empty;
        }

        /// <summary>
        /// Gets the currently installed WSL version (*version only*) (async: Task)
        /// </summary>
        /// <returns>Success: WSL Version; Fail: Empty string.</returns>
        public static string GetVersion() {

            string strOutput = string.Empty;

            Task task = Task.Run(() => {

                Debug.WriteLine($"WSL.GetVersion(): Task started (ID: {Task.CurrentId})");

                // Run the command
                strOutput = Misc.RunCommand("wsl.exe", "--version");

                Debug.WriteLine($"WSL.GetVersion(): Task complete (ID: {Task.CurrentId})");

            });

            task.Wait();

            string start = "wsl version: ";
            string end = Environment.NewLine;

            // Try to parse out a version number
            int startIndex = strOutput.ToLower().IndexOf(start) + start.Length;
            int endIndex = strOutput.IndexOf(end, startIndex);
            string strResult = strOutput.Substring(startIndex, endIndex - startIndex);

            // TODO: Should be beefed up with validation (make sure an actual version number was fetched)
            return strResult.Length > 0 ? strResult : string.Empty;

        }

        /// <summary>
        /// Gets the currently installed WSL version (*version only*) (async: BackgroundWorker)
        /// </summary>
        /// <returns>Success: WSL Version; Fail: Empty string.</returns>
        public static void GetVersion_BGW(Action<string> callback) {

            // Example usage:
            //
            // WSL.GetVersion_BGW((strVer) => {
            //    Debug.WriteLine(strVer);
            // });

            BackgroundWorker worker = new();

            worker.DoWork += (sender, e) => {

                string strOutput = string.Empty;

                // Run the command
                strOutput = Misc.RunCommand("wsl.exe", "--version");

                string start = "wsl version: ";
                string end = Environment.NewLine;
                string strVersion = string.Empty;

                try {

                    // Try to parse out a version number
                    int startIndex = strOutput.ToLower().IndexOf(start) + start.Length;
                    int endIndex = strOutput.IndexOf(end, startIndex);
                    strVersion = strOutput.Substring(startIndex, endIndex - startIndex);

                } catch (Exception ex) {
                    Debug.WriteLine($"GetVersion_BGW(): {ex.Message}");
                }

                e.Result = !string.IsNullOrEmpty(strVersion) ? strVersion : string.Empty;

            };

            worker.RunWorkerCompleted += (sender, e) => {

                if (e.Error != null) {
                    // Handle errors
                } else {

                    // Ex:
                    string strVersion = e.Result as string;
                    callback?.Invoke(strVersion);

                }

            };

            worker.RunWorkerAsync();
        
        }

        /// <summary>
        /// Fetches the current WSL IP
        /// </summary>
        /// <returns>Success: (string) IP Address; Fail: Empty string</returns>
        public static string GetIP_Task() {

            // TODO: Does running this START WSL if it's not already running?
            // TODO: Need to update this to fit the new ParseIP(out strOutput, out strWSLIP);

            string strOutput = string.Empty;
            string strWSLIP = string.Empty;

            Task task = Task.Run(() => {

                //Debug.WriteLine($"WSL_GetIP(): Task started (ID: {Task.CurrentId})");

                // Run the command in Bash
                //strOutput = Misc.RunCommand("bash.exe", string.Format("{0} {1}", "-c", "\"ifconfig eth0 | grep 'inet '\""));
                strOutput = Misc.RunCommand("bash.exe", $"{"-c"} {"\"ifconfig eth0 | grep 'inet '\""}");
                //strOutput = ParseIP(out strOutput, out strWSLIP);

                // Try to parse out a valid IPv4
                strWSLIP = IP_Regex.IPv4RegEx.Match(strOutput).ToString();  
                
                //Debug.WriteLine($"WSL_GetIP(): Task complete (ID: {Task.CurrentId})");
            });

            task.Wait();

            return !string.IsNullOrEmpty(strWSLIP) ? strWSLIP : string.Empty;

        }

        /// <summary>
        /// Fetches the current WSL IP (async: Task)
        /// </summary>
        /// <returns>Success: (string) IP Address; Fail: Empty string</returns>
        public static async Task<string> GetIP_Task_Async() {

            // TODO: Need to update this to fit the new ParseIP(out strOutput, out strWSLIP);

            string strOutput = string.Empty;
            string strWSLIP = string.Empty;

            await Task.Run(async () =>
            {
                //Debug.WriteLine($"WSL_GetIP: Task started (ID: {Task.CurrentId})");

                // Run the command in Bash
                //strOutput = await Misc.RunCommandAsync("bash.exe", string.Format("{0} {1}", "-c", "\"ifconfig eth0 | grep 'inet '\""));
                strOutput = await Misc.RunCommandAsync("bash.exe", $"{"-c"} {"\"ifconfig eth0 | grep 'inet '\""}");
                //strOutput = await ParseIP(out strOutput, out strWSLIP);

                // Try to parse out a valid IPv4
                strWSLIP = IP_Regex.IPv4RegEx.Match(strOutput).ToString();

                //Debug.WriteLine($"WSL_GetIP: Task complete (ID: {Task.CurrentId}) Result: {strWSLIP}");
            });

            return !string.IsNullOrEmpty(strWSLIP) ? strWSLIP : string.Empty;
        }

        /// <summary>
        /// Fetches the current WSL IP (async: BackGroundWorker)
        /// </summary>
        /// <returns>Success: (string) IP Address; Fail: Empty string</returns>
        /// <remarks>Ex. Usage: WSL.GetIP_BGW((ip) => lblWSLIP.Text = $"WSL IP: {ip}");</remarks>
        public static void GetIP_BGW(Action<string> callback) {

            // Example usage:
            //
            // WSL.GetIP_BGW((ip) => {
            //    Debug.WriteLine(ip);
            // });

            BackgroundWorker worker = new();

            worker.DoWork += (sender, e) => {

                string strOutput = string.Empty;
                string strWSLIP = string.Empty;

                ParseIP(out strOutput, out strWSLIP);

                e.Result = strWSLIP.Length > 0 ? strWSLIP : string.Empty;

            };

            worker.RunWorkerCompleted += (sender, e) => {

                if (e.Error != null) {
                    // Handle errors
                } else {

                    string ip = e.Result as string;
                    callback?.Invoke(ip);

                }

            };

            worker.RunWorkerAsync();
        }

        /// <summary>
        /// Scans WSL for any listening ports, and stores them in a dictionary collection. (async: BackgroundWorker)
        /// </summary>
        /// <param name="callback">Returns a Dictionary collection</param>
        public static void GetListeningPorts_BGW(Action<Dictionary<string, string>> callback) {

            // Example usage:
            //
            // Create a Callback function
            // private void DiscoveredPorts_Callback(Dictionary<string, string> portsDic) {
            //
            //     foreach (var kvp in portsDic) {
            //         Debug.WriteLine($"IP: {kvp.Key}, Port: {kvp.Value}");
            //     }
            //
            // }
            //
            // Place call wherever:
            // WSL.GetListeningPorts_BGW(DiscoveredPorts_Callback);

            BackgroundWorker worker = new();

            worker.DoWork += (sender, e) => {

                string strOutput = string.Empty;
                Dictionary<string, string> dicPorts;

                ParseListeningPorts(out strOutput, out dicPorts);

                e.Result = dicPorts.Count > 0 ? dicPorts : null;

            };

            worker.RunWorkerCompleted += (sender, e) => {

                if (e.Error != null) {
                    // Handle errors
                } else {

                    Dictionary<string, string> dicPorts = e.Result as Dictionary<string, string>;
                    callback?.Invoke(dicPorts);

                }

            };

            worker.RunWorkerAsync();
        }

        /// <summary>
        /// Parses the IP (or at least tries to) out of WSL.
        /// </summary>
        /// <param name="strOutput">Contains the raw result of the bash command</param>
        /// <param name="strWSLIP">Contains the parsed out IP on success, or empty string on failure, to test against.</param>
        private static void ParseIP(out string strOutput, out string strWSLIP) {

            bool bFound = false;

            // Ubuntu
            strOutput = Misc.RunCommand("bash.exe", $"-c {"\"ifconfig eth0 | grep 'inet '\""}");

            // Check if the "Ubuntu" ifconfig worked; if not, try using Debian's ip addr instead.
            // TBH: Originally I just had the Ubuntu ver and it was fine, but later read that Debian doesnt have that by default, so added it in for redundancy.
            //      Technically, the ip addr ver works on both systems during my tests, but since I already coded ifconfig in, I'm leaving it for posterity;
            //      can't hurt anything and at least this way I don't feel like I wasted my time. ;)
            if (IP_Regex.IPv4RegEx.Match(strOutput).Success) {
                bFound = true;
            } else {

                // For Debian, which does not come with ifconfig out of the box, you can try:
                strOutput = Misc.RunCommand("bash.exe", $"-c {"\"ip addr | grep -Ee 'inet.*eth0'\""}");

                if (IP_Regex.IPv4RegEx.Match(strOutput).Success) {
                    bFound = true;
                }

            }

            if (bFound) {
                strWSLIP = IP_Regex.IPv4RegEx.Match(strOutput).ToString();
                return;
            }
            strWSLIP = string.Empty;
        }

        /// <summary>
        /// Parses out the listening ports; both IP4 and IP6 (in the Value). Also stores the IP(s) in the Key.
        /// </summary>
        /// <param name="strOutput">The string containing the list of IP:Port to parse</param>
        /// <param name="dicPorts">Dictionary to store the results</param>
        private static void ParseListeningPorts(out string strOutput, out Dictionary<string, string> dicPorts) {

            dicPorts = new();

            // Here just to prevent dupe key error in the dictionary
            int intMatchCount = 0;

            // Ubuntu
            strOutput = Misc.RunCommand("bash.exe", "-c \"netstat -lnt | awk '{print $4}'\"");

            // Match all IPv4 ports
            MatchCollection matches = IP_Regex.IPv4PortRegEx.Matches(strOutput);

            // Add all IP4 Ports to collection
            foreach (Match match in matches) {
                dicPorts.Add($"IPv4 [{intMatchCount}] [{match.Groups[1].Value}]", match.Groups[3].Value);
                intMatchCount++;
            }

            // Reset
            intMatchCount = 0;

            // Match all IPv6 ports
            matches = IP_Regex.IPv6PortRegEx.Matches(strOutput);

            // Add all IP6 Ports to collection
            foreach (Match match in matches) {
                dicPorts.Add($"IPv6 [{intMatchCount}] [{match.Groups[1].Value}]", match.Groups[2].Value);
                intMatchCount++;
            }

        }

        /// <summary>
        /// Shut down WSL (async)
        /// </summary>
        /// <param name="bShowResult">[optional: default True] Shows a messagebox confirming WSL has shut down</param>
        public static void ShutDown(bool bShowResult = true) {

            if (IsRunning()) {

                if (MessageBox.Show("Are sure you want to shut WSL down?", "WSL: Shutdown", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes) {
            
                    Task task = Task.Run(() => {

                        Debug.WriteLine($"WSL.ShutDown(): Task started (ID: {Task.CurrentId})");

                        // Run the shutdown command
                        Misc.RunCommand("wsl.exe", "--shutdown");

                        Debug.WriteLine($"WSL.ShutDown(): Task complete (ID: {Task.CurrentId})");

                    });

                    // TODO: Add a timer here to timeout if there's a problem shutting down WSL, so this loop doesn't end up running perpetually.
                    while (IsRunning()) {
                        // Loop until WSL is confirmed to be shut down, then show them confirmation.
                        // TODO: Add some sort of user feedback here to let them know we're doin work?
                    }

                    task.Wait();

                    if (bShowResult) {
                        MessageBox.Show("WSL is shut down.", "WSL: Shutdown", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                }

            } else {

                // Ya'll tryna shut down somethin that ain't even runnin!
                MessageBox.Show("WSL isn't running.", "WSL: Shutdown", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

        }

        /// <summary>
        /// Start WSL (async: Task)
        /// </summary>
        /// <param name="bShowResult">[optional: default True] Shows a messagebox confirming WSL has started</param>
        public static void Start(bool bShowResult = true) {

            if (!IsRunning()) {

                Task task = Task.Run(() =>
                {
                    Debug.WriteLine($"WSL_Start: Task started (ID: {Task.CurrentId})");

                    // Run the startup command
                    Misc.RunCommand("wsl.exe", string.Empty, string.Empty, false);

                    Debug.WriteLine($"WSL_Start: Task complete (ID: {Task.CurrentId})");
                });

                // TODO: Add a timer here to timeout if there's a problem starting up WSL, so this loop doesn't end up running perpetually.
                while (!IsRunning()) {
                    // Loop until WSL is confirmed to be running, then show them confirmation.
                    // TODO: Add some sort of user feedback here to let them know we're doin work?
                }

                task.Wait();

                if (bShowResult) {
                    MessageBox.Show("WSL is now running.", "WSL: Startup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

            } else {

                // Ya'll tryna start up somethin that's already runnin!
                MessageBox.Show("WSL is already running.", "WSL: Startup", MessageBoxButtons.OK, MessageBoxIcon.Information);

            }

        }

        /// <summary>
        /// Restart WSL
        /// </summary>
        /// <param name="bShowResult">[optional: default True] Shows a messagebox confirming WSL has restarted</param>
        public static void Restart(bool bShowConfirmation = true, bool bShowResult = true) {

            bool bRunIt = false;

            // Decide whether to run the Restart:
            //  - Defaults to not running
            //  - Sets flag to run only if dev chose NoConfirmation, OR if dev chose to confirm first AND user said Yes to confirmation dialog. If user chose No on dialog, RunIt flag stays at false.
            if ((MessageBox.Show("Are sure you want to restart WSL?", "WSL: Restart", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes && bShowConfirmation) || !bShowConfirmation) {
                bRunIt = true;
            }

            // Do the acutal restart if all is Go
            if (bRunIt) {

                Misc.RunCommand("wsl.exe", "--shutdown");

                Debug.WriteLine("WSL.Restart(): Shutdown command sent");

                while (IsRunning()) {
                    // Loop until WSL is confirmed to be shut down, then start WSL again.
                    // TODO: Add some sort of user feedback here to let them know we're doin work? (i.e. a Wait cursor, or "shutting down WSL" text.)
                }

                Debug.WriteLine("WSL.Restart(): WSL has shutdown");

                // Run the startup command
                Misc.RunCommand("wsl.exe", string.Empty, string.Empty, false);

                Debug.WriteLine("WSL.Restart(): Startup command sent");

                while (!IsRunning()) {
                    // Loop until WSL is confirmed to be running, then show them confirmation.
                    // TODO: Add some sort of user feedback here to let them know we're doin work? (i.e. a Wait cursor, or "starting WSL" text.)
                }

                // Do 1 last check to confirm
                bool isRunning = IsRunning();

                Debug.WriteLine($"WSL.Restart(): WSL {(isRunning ? "has restarted." : "had some issue restarting.")}");

                if (bShowResult) {

                    MessageBox.Show($"WSL {(isRunning ? "has restarted." : "had some issue restarting.")}", "WSL: Restart", MessageBoxButtons.OK, isRunning ? MessageBoxIcon.Information : MessageBoxIcon.Error);

                }

            }

        }

        public static void Restart_BGW(Action<bool> callback, bool bShowConfirmation = true, bool bShowResult = true) {

            // TODO: Find out why using this BGW still causes the main GUI to freeze while it's doing it's work ...
            //       Also, looks like it's throwing up multiple result mboxes when done.

            // Example usage:
            //
            // WSL.Restart_BGW((result) => {}, true, true);

            bool bRunIt = false;

            // Decide whether to run the Restart:
            //  - Defaults to not running
            //  - Sets flag to run only if dev chose NoConfirmation, OR if dev chose to confirm first AND user said Yes to confirmation dialog. If user chose No on dialog, RunIt flag stays at false.
            if ((MessageBox.Show("Are sure you want to restart WSL?", "WSL: Restart", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes && bShowConfirmation) || !bShowConfirmation) {
                bRunIt = true;
            }

            // Do the acutal restart if all is Go
            if (bRunIt) {

                BackgroundWorker worker = new();

                worker.DoWork += (sender, e) => {

                    Misc.RunCommand("wsl.exe", "--shutdown");

                    Debug.WriteLine("Restart_BGW(): Shutdown command sent");

                    while (IsRunning()) {
                        // Loop until WSL is confirmed to be SHUT DOWN.
                        // TODO: Add some sort of user feedback here to let them know we're doin work? (i.e. a Wait cursor, or "shutting down WSL" text.)
                    }

                    Debug.WriteLine("Restart_BGW(): WSL has shutdown");

                    // Run the startup command
                    Misc.RunCommand("wsl.exe", string.Empty, string.Empty, false);

                    Debug.WriteLine("Restart_BGW(): Startup command sent");

                    while (!IsRunning()) {
                        // Loop until WSL is confirmed to be RUNNING.
                        // TODO: Add some sort of user feedback here to let them know we're doin work? (i.e. a Wait cursor, or "starting WSL" text.)
                    }

                    // Do 1 last check to confirm, and pass it onto RunWorkerCompleted.
                    bool isRunning = IsRunning();
                    e.Result = isRunning;

                };

                worker.RunWorkerCompleted += (sender, e) => {

                    if (e.Error != null) {
                        // Handle errors
                    } else {

                        bool isRunning = (bool)e.Result;

                        Debug.WriteLine($"WSL.Restart_BGW(): WSL {(isRunning ? "has restarted." : "had some issue restarting.")}");

                        if (bShowResult) {
                            MessageBox.Show($"WSL {(isRunning ? "has restarted." : "had some issue restarting.")}", "WSL: Restart", MessageBoxButtons.OK, isRunning ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                        }

                        callback?.Invoke(isRunning);

                    }

                };
                worker.RunWorkerAsync();
            }

        }

    }
}
