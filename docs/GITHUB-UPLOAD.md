# Publish ExaltHelper

Suggested repository name: **ExaltHelper**

Suggested description:

> A Windows companion for Realm of the Mad God Exalt with DPS tracking, a combat HUD, player and party inspection, loot logs, Moonlight Village tracking, and configurable gameplay utilities.

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

Create a GitHub release with tag **v1.0.0** and title **ExaltHelper v1.0.0**. Attach `ExaltHelper-Release.zip` for users to download.

Suggested release description:

> ExaltHelper brings DPS tracking, encounter history, player and equipment inspection, party and loot logs, and Moonlight Village tools to a configurable in-game HUD. Includes launcher integration and gameplay utilities.
>
> Download and extract `ExaltHelper-Release.zip`, then run `ExaltHelper.exe`. Requires Windows, .NET Framework 4.8, and Realm of the Mad God Exalt. F8 toggles the HUD; F9 toggles click-through.

Dependency DLLs, compiled UI resources, game data, and the native hook are required repository inputs. The `.gitignore` excludes build output, runtime settings, diagnostics, and packet captures.
