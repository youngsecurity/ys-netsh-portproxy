
## v2.0.0

- **General**

	- Bumped .NET Core version up to 7 (from 6)
	- Changed app color scheme to a dark Nord scheme
	- Renamed app from PortProxyGUI to PortProxyGooey. I've done so many changes to the original that it was doubtful the original repo would want to pull all of the changes, so I'm leaving it as it's own forked entity. This allows me to make whatever changes I want w/out worrying about breaking something the original owner may have wanted to keep a particular way. Credit to the original owner will always stay in place.
	- App now also saves/restores last window position
	- Changed main window color (again) to a lighter Nord scheme, so the Listview Group labels are actually readable, and the annoying MS devs don't give us an easy way to change their colors.
	- Moved the FlushDNS code to PortProxyUtil.cs and removed the DNSUtil.cs from project; less files = less confusion.
	- Moved some other functions over to PortProxyUtil.cs as well, to make that class a sort of "general use" class (i.e. the Launch function).
	- Added a var on the main form to set the repo URL app-wide. Makes it easier if ever wanting to change it, for other forks, and stuff. Also, changed some of the "hardcoded" repo URLS already there, to call from that main form var.
	- Added menu itm for links to external sites, for links to network tools or what-have-you.
	- Added Port Forwarding Tester to above menu list item
	- FlushCache now uses LibraryImport instead of DllImport. Reduced overhead and better performance.
	- Added minimum window dimensions, so things to get lookin too funky if user gets too froggy.
	- Added proxy count label
	- Added some bling, to gussy the UI up a bit. Gotta make it Gooey.
	- Added more items to double-click on to open the New Item dialog
	- Moved WSL-specific functions to their own WSL class, for portability.
	- Added more menu icons
	- Added Winsock Reset
    - Added WSL functions: Is WSL Running, ShutDown, Start, Restart.

- **UI**

	- Added Version to Titlebar
	- Added tooltips to most menu items
	- Replaced previous Shortcuts with real shortcuts (incl. changing some of the shortcuts themselves to better match their function, and standard convention)
	- Moved Refresh menu items lower in the menu, and moved the direct item actions up.
	- Renamed "Modify" to "Edit"
	- Added some icons to context menu

- **External Apps**

	- Added External Apps menu, as a handy place to open anything external
	- Added Windows Firewall
	- Added MalwareBytes Windows Firewall Control
	- Added Windows Network Adapters

- **FlushDNS**

	- Added FlushDNS confirmation
	- Moved FlushDNS to "More" menu (trying to keep "item" related stuff on the main menu, and extra stuff in the More menu).

- **About Form**

	- Changed back color and font to match rest of app
    
- **Code**

	- Added some regions, to make things easier to read.
	- Replaced generic vars with explicit vars
	- Moved 'Add New Item' to it's own function so it can be called from multiple places w/out adding code noise.
	- Bumped .NET runtime from - to 4.8.1 mostly because it wouldn't load on my machine until I did. ;) (Forgot to mention that in the last commit).
	- Change all instances of listview1 to listViewProxies to avoid confusion
	- Added a confirmation when deleting proxies
	- Add delete count to context menu (only shows when more than 1) to let users know how many they're about to nuke
	- Simplified many var declarations
	- Added more hotkeys
	- Moved database from the User's Documents folder to the ProgramData folder. [Closes](https://github.com/zmjack/PortProxyGUI/issues/9#issuecomment-1049251718)

- **Import / Export**

	- Moved Import/Export menu items to their own submenu under the "more" menu. (a.k.a. Backup / Restore)
	- Beefed up Import dialog (title, filename)
	- Beefed up Exporting: Autogenerate a backup name; provide user feedback of success or failure.
	- Added confirmation before importing, in case someone decides to back out and not ruin their current list.

- **Set Proxy**

	- Locked "Type" combobox so user can't edit it. (don't think they need to considering the options are limited?)
	- Changed "Listen On" to a combobox, to allow adding in some basic autocomplete suggestions/appends
	- Added autocomplete suggestions/append to Groups with some basic suggestions
    - Added autocomplete suggestions/append to ports with some basic suggestions
    - UI Refactoring
	- When filling in a new item; filling in a port dupes that port to the other port fields so user doesn't have to type it multiple times.
	- Sets focus to the Type field if for some reason it can't be detected. Helps the user out just that little bit more.
	- Can now add a range of "Listening On" ports. [Closes](https://github.com/zmjack/PortProxyGUI/issues/11)
	- App now fetches the current WSL IP address. It'll display it in the statusbar, as well as add it to the autocomplete fields. Double clicking the label will re-fetch the IP to check if it changed (not likely).
	- IPv4 is also validated now.
	- Duplicate checking now. If trying to add a duplicate proxy (whether a single proxy or a range) you'll be notified. Also fixes an exception/bug when trying to add a dupe.
	- Fixed IPv6 validation regex that previously appears to have been for detecting MAC addresses rather than IP6 IP's.
	- Type validation now also automatically happens whenever user enters anything into their respective fields.
	- Removed previous Type validation upon actual submission. No longer needed after other changes.
	- No longer required to manually select the Type; it happens automatically for you. Thus the selection box has been removed.
	- Added ability to use mousewheel in port textboxes to increase/decrease the port number.
	- Added ability to do the same with the keyboard arrow up/down
	- Port textboxes now only allow numbers
	- IP fields now only allow numbers, period/dot, asterisk
	- Added "non-destructive" auto-labeling of common ports as a comment when user enters in a known port. Non-destructive meaning if user manually typed anything in already, their comment won't get deleted when the auto-label is appended.
    - * Future enhancement idea of this could be to remove it as an appended auto-comment, and add it as perhaps an icon, or in its own label-column, or whatever else comes to mind.
    - Fixed listenon and connectto field validations to allow colon (for IPv6) and ctrl+v pasting.

- **ListView**

	- Clicking an empty space opens the New Item dialog
	- Clicking the first column in an item (it's enabled/disabled icon) now toggles it's state
	- Added cursor changing while refreshing list, for some user feedback.
    - Column Sorting is now persistent as well.
    - Added ability to view and/or copy the actual netsh add/delete commands for the selected item
	- Changed Single-click to Double-click to open the New Proxy dialog when clicking an open space in the listview.
	- Added ability to clear the whole list in a single click (delete all proxies)
	- Added ability to open the selected item in the registry (it's entire Type actually, but who's counting?). As of right now the app doesn't do any validation, or refreshing, or anything at all if user manually edits anything in it and exits the editor. Maybe some other time.
	- After adding a new proxy(s), the last-added one will be set .EnsureVisible and .Selected as a convenience (in case of a long list and it were to scroll out of view, or something).
	- Added ability to clone a proxy, to make simple changes like only a port change easy, instead of having to completely fill out a new form. (incl. hotkey)
    - Can now Rename groups