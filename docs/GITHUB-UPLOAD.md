# Publish ExaltHelper

Suggested repository name: **ExaltHelper**

Suggested description:

> A Windows companion for Realm of the Mad God Exalt with compact and desktop HUD modes, missions and quests, account tools, combat/loot/chat history, and Moonlight Village tracking.

## Create the repository

The source includes a README and `.gitignore`. When creating the repository on GitHub, leave **Add a README file** and **Add .gitignore** disabled to use the supplied files.

Upload the contents of `ExaltHelper-GitHub`, with `README.md` and `ExaltHelper.csproj` at the repository root. Use GitHub Desktop or Git: `ExaltHelper_Data/Objects.xml` exceeds GitHub's browser upload limit, but is below the 100 MB limit for Git pushes.

### GitHub Desktop

1. Choose **File > Add local repository** and select the source folder.
2. If prompted, choose **Create a repository here**, keeping the supplied README and `.gitignore`.
3. Commit the source files.
4. Choose **Publish repository** and enter the name, description, and visibility.

### Git over HTTPS

Create an empty GitHub repository, then run these commands in the source folder. Replace `YOUR-USERNAME` with your GitHub username.

```powershell
git init -b main
git add .
git commit -m "Initial ExaltHelper release"
git remote add origin https://github.com/YOUR-USERNAME/ExaltHelper.git
git push -u origin main
```

## Create the release

Create a GitHub release with tag **v1.1.0** and title **ExaltHelper v1.1.0**. Attach `ExaltHelper-1.1.0-Release.zip` for users to download.

Suggested release description:

> ExaltHelper 1.1 adds a resizable desktop HUD for a second monitor, persistent widgets/taskbar, missions/quests, account and collection views, searchable combat/loot/chat history and a theoretical DPS calculator. Includes the existing compact HUD, launcher and Moonlight Village tools.
>
> Download and extract `ExaltHelper-1.1.0-Release.zip`, then run `ExaltHelper.exe`. Requires Windows, .NET Framework 4.8, and Realm of the Mad God Exalt. F8 toggles the active HUD; F9 locks the compact overlay; F10 switches desktop/compact modes. See the included HUD guide and verification ledger for offline test evidence and live-game validation limits.

Dependency DLLs, compiled UI resources, game data, and the native hook are required repository inputs. The `.gitignore` excludes build output, runtime settings, diagnostics, and packet captures.
