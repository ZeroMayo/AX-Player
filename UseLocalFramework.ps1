# Run this script to use a local copy of osu-framework rather than fetching it from nuget.
# It expects the osu-framework directory to be at the same level as the osu directory
#
# https://github.com/ppy/osu-framework/wiki/Testing-local-framework-checkout-with-other-projects

$APP_CSPROJ="AXPlayer.App/AXPlayer.App.csproj"
$SLN="AXPlayer.sln"

dotnet remove $APP_CSPROJ reference ZeroMayo.Framework;

dotnet sln $SLN add ../osu-framework/osu.Framework/osu.Framework.csproj `
    ../osu-framework/osu.Framework.NativeLibs/osu.Framework.NativeLibs.csproj;

dotnet add $APP_CSPROJ reference ../osu-framework/osu.Framework/osu.Framework.csproj;