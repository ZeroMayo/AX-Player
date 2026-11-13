## Localization tips
If you writes UI Text, must register string key to `AXPlayerStrings.cs`.
If you edit `AXPlayerStrings.cs`, Fill in the added or edited translations in the AXPlayer.resx and AXPlayer.[language].resx files in the Localization folder of AXPlayer.App.Resources with the ones found at AXPlayerStrings.cs.

## Testing instructions
- Run `dotnet build <project_name>` to check build status for that app.
- Fix any errors until the whole build status is passed.
- Add or update visual tests at `AXPlayer.App.Tests/Visual` directory for the code you change, even if nobody asked.