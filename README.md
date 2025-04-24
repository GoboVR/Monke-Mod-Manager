# Monke Mod Manager
![image](https://github.com/user-attachments/assets/4f25cf45-7c83-4781-9c8c-61eff36e18ca)


This program will install custom mods into Gorilla Tag automatically, and can be re-run in order to update the mods

This uses the github api to get the latest release of all these mods, so you know you'll always be getting the latest version!
(If there is a mod that you have made that you want added to MMM, send me or Graze a message or ping us somewhere on Discord! `ngbatzyt` or `the.graze`)

Uses [MonkeUpdater](https://github.com/NgbatzYT/MonkeUpdater) to update mmm automatically.

## To have your Theme/Cosmetic etc added.
DM me (`ngbatzyt`):
* The file and images of the thing
* What mod it uses the name of it and the author name.

## To have your mod added
DM me:
* The Github repository of the mod you want added 
* Any dependencies

Or fork => [this](https://github.com/The-Graze/MonkeModInfo) <= repository and add it yourself with a PullRequest

Mod submission is likely subject to change in the future.

### Ensure that
* your mod is built in the monke mod manager compatible format through `MakeRelease.ps1` (replace `netstandard2.0` with `netstandard2.1` in the code) or you use just a DLL in your release
* you list the correct dependencies
* your mod is fully working

### MMM Install files
These files are like modpacks and can install any mod(s) without it needing to be on mmm.
They can contain Cosmetics, Themes, Mods, anything really.

You can find the MMMInstaller file maker [here](https://github.com/ngbatzyt/MMMInstallerFile/releases/latest).
