using Spectre.Console;
using System.Diagnostics;
using System.Security.AccessControl;
using Utils;
using YamlDotNet.Serialization;

namespace QuickSearch;

#pragma warning disable IDE1006
public class Program
{
    public static Args inpArgs = new();
    public static bool Debug = false;
    public static void Main(string[] args)
    {
        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()))
            log("This app will only work with Windows or Linux", 3, true);

        if (args.Length is 0)
            log("No arguments provided", 3, true, true);

        if (args[0] is "DEB_ENA")
            Debug = true;

        // Setup Args
        if (args.Contains("--reset-settings"))
        {
            TrySep(":", out string action, out string confirmation);
            if (AnsiConsole.Confirm("Are you sure you want to reset the QuickSearch settings file?\r\nThis will reset any settings to their defaults.") || confirmation is "y")
            {
                WriteSettings(inpArgs);
                log("Settings reset.");
            }
            else
                log("Exiting...");
            Environment.Exit(0);
        }

        try
        {
            ReadSettings(out inpArgs);
            if (inpArgs is null)
            {
                log("Settings file is empty. Proceeding with default values", 2);
                inpArgs = new();
            }

            if (args.Contains("--list-all"))
            {
                foreach (var setting in inpArgs.GetType().GetFields())
                    if (setting.GetValue(inpArgs).ToString() is not "System.String[]")
                        AnsiConsole.MarkupLine($"[{cc.neon}]{setting.Name}[/]: {setting.GetValue(inpArgs)}");
                    else
                    {
                        AnsiConsole.MarkupLine($"[{cc.neon}]{setting.Name}[/]:");
                        foreach (string set in (string[])setting.GetValue(inpArgs))
                            AnsiConsole.WriteLine($"  {set}");
                    }


                Environment.Exit(0);
            }
        }
        catch (FileNotFoundException)
        {
            log($"Could not find '{InAppArgs.PathToSettings()}'. Proceeding with default values", 2);
            inpArgs = new();
        }
        catch (YamlDotNet.Core.YamlException)
        {
            log($"Settings file is either malformed or corrupt. Run '--reset-settings' or edit it manually to fix the issue", 3, true, true);
        }
        catch (Exception ex)
        {
            log($"Unknown error while loading 'qs-settings.yaml'\r\n{ex.Message}", 3, true, true);
        }

        bool updateSettings = false;
        bool forceSearch = true;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--")
                break;
            if (arg.Contains(':'))
            {
                // Gets or sets the settings in the .yaml file based on the value of 'va'
                TrySep(arg, out string setting, out string value);
                args[i] = "";
                switch (setting)
                {
                    case "--pref-search-engine":
                        if (value is null)
                            AnsiConsole.MarkupLine($"preferred search engine: {inpArgs.PreferredSearchEngine}");
                        else
                            inpArgs.PreferredSearchEngine = value;
                        break;
                    case "--pref-profile":
                        if (value is null)
                            AnsiConsole.MarkupLine($"preferred profile: {inpArgs.PreferredProfile}");
                        else
                            inpArgs.PreferredProfile = value;
                        break;
                    case "--pref-site-list":
                        if (value is null)
                        {
                            for (int x = 0; x < inpArgs.SpecifiedSiteList.Length; x++)
                                AnsiConsole.MarkupLine($"site {x}: {inpArgs.SpecifiedSiteList[x]}");
                            Environment.Exit(0);
                        }
                        else
                        {
                            List<string> tmp = new();
                            if (value.Contains(','))
                            {
                                foreach (string site in value.Split(','))
                                    tmp.Add(site);
                                inpArgs.SpecifiedSiteList = tmp.ToArray();
                            }
                            else
                                inpArgs.SpecifiedSiteList = new string[] { value };
                        }
                        break;
                    default:
                        log($"Can't parse property '{setting}'\r\n{helpText}", 3);
                        return;
                }
                updateSettings = true;
                forceSearch = false;
            }
            else if (arg.StartsWith("-"))
            {
                string setting = args[i];
                string value = "";
                if (args.Length < i + 1)
                {
                    value = args[++i];
                    args[i - 1] = "";
                }

                args[i] = "";
                switch (arg)
                {
                    // Sets temporary settings for single use
                    case "-S":
                    case "--search-engine":
                        inpArgs.PreferredSearchEngine = value;
                        break;
                    case "-p":
                    case "--profile":
                        inpArgs.PreferredProfile = value;
                        break;
                    case "-s":
                    case "--site-list":
                        List<string> tmp = new();
                        if (value.Contains(','))
                        {
                            foreach (string site in value.Split(','))
                                tmp.Add(site);
                            inpArgs.SpecifiedSiteList = tmp.ToArray();
                        }
                        else
                            inpArgs.SpecifiedSiteList = new string[] { value };
                        break;
                    case "-G":
                    case "--generate-settings":
                        if (updateSettings && !inpArgs.HideTips)
                            AnsiConsole.MarkupLine($"[cyan]Tip[/]: using '{setting}' is redundant when setting a property");
                        updateSettings = true;
                        forceSearch = false;
                        break;
                    case "--help":
                        AnsiConsole.MarkupLine(helpText);
                        return;
                    default:
                        log($"Can't parse switch '{arg}'", 3, true, true);
                        break; // Break wont do anything, but is needed to compile
                }
            }
        }

        if (updateSettings)
            WriteSettings(inpArgs);

        if (!forceSearch)
            return;

        if (inpArgs.PreferredSearchEngine is "")
            log("No prefered search engine. Use '--pref-search-engine:' to set it", 3, true);

        if (inpArgs.PreferredProfile is "")
            log("No prefered profile. Use ''--pref-profile:' to set it", 3, true);

        string sitelist = "";
        if (inpArgs.SpecifiedSiteList.Length is not 0)
        {
            sitelist = "+";

            string[] tmp = new string[args.Length];
            for (int i = 0; i < tmp.Length; i++)
                if (args[i] is not "")
                    tmp[i] = args[i];

            args = tmp;

            foreach (string site in inpArgs.SpecifiedSiteList)
                sitelist += "site:" + site + "+OR+";
            sitelist = sitelist[..^4];
        }

        args.Shrink();
        if (args.Length is 1 or 0)
            log("No search terms entered", 3, true);

        string searchTerms = "";
        foreach (string arg in args)
            searchTerms += arg + "+";
        searchTerms = searchTerms[..^1];

        var searchLink = $"\"https://google.com/search?q={searchTerms}{sitelist}\"";

        if (!File.Exists(inpArgs.PreferredSearchEngine))
            log($"Failed to find search engine app with path '{inpArgs.PreferredSearchEngine}'", 3, true);

        Action StartApp = () => Process.Start(new ProcessStartInfo()
        {
            FileName = inpArgs.PreferredSearchEngine,
            WorkingDirectory = OperatingSystem.IsLinux() ? "/" : Directory.GetCurrentDirectory(),
            Arguments = $"--profile-directory=\"{inpArgs.PreferredProfile}\" {searchLink}",
        });

        if (!Debug)
            WaitFor("Starting search engine...", StartApp).GetAwaiter().GetResult();
        else
        {
            AnsiConsole.MarkupLine($"Site: {searchLink}\r\n" +
            $"App:  {inpArgs.PreferredSearchEngine}\r\n" +
            $"Prof: {inpArgs.PreferredProfile}\r\n" +
            $"");
        }
    }

    public static string helpText =
    $@"Usage: [{cc.violet}]qs[/] [[options]] [[arguments]]

[magenta]Permanent options:[/]
  [{cc.neon}]--pref-search-engine[/] [indianred1]<path>[/]    [{cc.white}]Set the preferred search engine[/]
  [{cc.neon}]--pref-profile[/] [indianred1]<profile>[/]       [{cc.white}]Set the preferred profile[/]
    [red3]WARNING:[/] The profile name must match the wanted profile. Some search engines will create a
    new profile if it doesnt already exist.
  [{cc.neon}]--pref-site-list[/] [indianred1]<sites>[/]       [{cc.white}]Set the preferred list of sites[/]
    [{cc.ceru}]Using a permanent option with no value prints the current value set to the console.[/]
      [magenta]Example:[/]
        [{cc.violet}]qs[/] [{cc.neon}]--pref-search-engine[/]:
        [[output]]: preferred search engine: path/to/app

[magenta]Temporary options:[/]
  [{cc.neon}]-S[/], [{cc.neon}]--search-engine[/] [indianred1]<path>[/]     [{cc.white}]Use a specific search engine for this query[/]
  [{cc.neon}]-p[/], [{cc.neon}]--profile[/] [indianred1]<profile>[/]        [{cc.white}]Use a specific profile for this query[/]
   └─[{cc.ceru}]Multiple sites should be comma separated and no spaces.[/]
      [magenta]Examples:[/]
        [{cc.violet}]qs[/] [{cc.neon}]-s[/] [{cc.vsky}]google.com,github.com,stackoverflow.com[/] [{cc.bleu}]something I want to search[/]
        [{cc.violet}]qs[/] [{cc.neon}]--pref-site-list[/]:[{cc.vsky}]google.com,github.com,stackoverflow.com[/] [{cc.bleu}]something I want to search[/]
  [{cc.neon}]-s[/], [{cc.neon}]--site-list[/] [indianred1]<sites>[/]        [{cc.white}]Use a specific list of sites for this query[/]
  [{cc.neon}]-G[/], [{cc.neon}]--generate-settings[/]        [{cc.white}]Generate a settings file for this tool[/] [[{InAppArgs.PathToSettings()}]]
  [{cc.neon}]--help[/]                         [{cc.white}]Display this help message[/]

[magenta]Other options[/]
  [{cc.neon}]--list-all[/]                     [{cc.white}]List all settings from {InAppArgs.PathToSettings()}[/]
  [{cc.neon}]--reset-settings[/]               [{cc.white}]Resets all settings to their default values[/]
   └─[{cc.ceru}]Use the value 'y' to bypass confirmation.[/]
      [magenta]Example:[/]
        [{cc.violet}]qs[/] [{cc.neon}]--reset-settings[/]:[indianred1]y[/]
     [{cc.ceru}]Otherwise, '--reset-settings' will promt for confirmation.[/]
  [{cc.neon}]--no-help[/]                      [{cc.white}]Shows this help menu on crash when enabled[/]
  [{cc.neon}]--no-tips[/]                      [{cc.white}]Gives tips to help improve command usage[/]
      

[magenta]Arguments:[/]
  [indianred1]<query>[/]                        [{cc.white}]The search query to perform[/]

Check for newer versions at [{cc.vsky}]https://github.com/Sombody101/QuickSearch[/]
";

    public static void log(string message, byte severity = 1, bool logAndExit = false, bool showHelpOnCrash = false)
    {
        AnsiConsole.MarkupLine($"qs: [" +
            $"{(severity is 1 ? cc.white : (severity is 2 ? "yellow" : c.Red))}]" +
            $"{(severity is 1 ? "message" : (severity is 2 ? "warning" : "fatal"))}[/]: " +
            $"{message}");
        if (showHelpOnCrash && inpArgs.ShowHelpOnCrash)
            AnsiConsole.MarkupLine(helpText);
        if (logAndExit)
            Environment.Exit(severity);
    }

    public static async Task WaitFor(string message, Action action)
        => AnsiConsole.Status().Spinner(Spinner.Known.Star).Start(message, ctx => { action(); });

    public static void WriteSettings(Args args)
    {
        try
        {
            if (!File.Exists(InAppArgs.PathToSettings()))
            {
                using FileStream fileStream = File.Create(InAppArgs.PathToSettings());
                if (OperatingSystem.IsWindows())
                {
                    FileSecurity fileSecurity = new();
                    fileSecurity.SetAccessRule(new("Everyone", FileSystemRights.ReadAndExecute | FileSystemRights.Write, AccessControlType.Allow));
                    fileStream.SetAccessControl(fileSecurity);
                }
                else
                    Process.Start("/bin/chmod", "666 " + InAppArgs.PathToSettings()).WaitForExit();
            }

            File.WriteAllText(InAppArgs.PathToSettings(), new SerializerBuilder().Build().Serialize(args));
        }
        catch (UnauthorizedAccessException)
        {
            log($"Failed to create '{InAppArgs.PathToSettings()}'. Either create the file using " +
                $"'> {InAppArgs.PathToSettings()}', or run qs " +
                $"{(OperatingSystem.IsWindows() ? "in an admin window" : "with sudo")}.", 3);
            Environment.Exit(1);
        }
        catch (Exception)
        {
            log($"Unkown error while creating or editing {InAppArgs.PathToSettings()}", 3);
            Environment.Exit(1);
        }
    }

    public static void ReadSettings(out Args args)
    {
        args = new DeserializerBuilder().Build().Deserialize<Args>(File.ReadAllText(InAppArgs.PathToSettings()));
    }

    public static bool TrySep(string input, out string firstPart, out string secondPart)
    {
        int delimiterIndex = input.IndexOf(':');

        if (delimiterIndex is -1)
        {
            firstPart = input;
            secondPart = null;
            return false;
        }

        firstPart = input[..delimiterIndex].Trim();
        secondPart = input[(delimiterIndex + 1)..].Trim();
        return true;
    }
}

public static class InAppArgs
{
    public static string PathToSettings() => OperatingSystem.IsWindows() ?
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
        "\\qs-settings.yaml" : "/etc/qs-settings.yaml";
}

public class Args
{
    public string PreferredSearchEngine = "";
    public string PreferredProfile = "Default";
    public string[] SpecifiedSiteList = { };
    public bool ShowHelpOnCrash = true;
    public bool HideTips = false;
}

public static class c
{
    public const string Black = "\u001b[30m";
    public const string Red = "#FF4B4B";
    public const string _Red = "\u001b[31m";
    public const string Green = "\u001b[32m";
    public const string Yellow = "\u001b[33m";
    public const string Blue = "\u001b[34m";
    public const string Magenta = "\u001b[35m";
    public const string Cyan = "\u001b[36m";
    public const string White = "\u001b[37m";
    public const string norm = "\u001b[0m";
    public const string BBlack = "\u001b[30;1m";
    public const string BGreen = "\u001b[32;1m";
    public const string BYellow = "\u001b[33;1m";
    public const string BBlue = "\u001b[34;1m";
    public const string BMagenta = "\u001b[35;1m";
    public const string BCyan = "\u001b[36;1m";
    public const string BWhite = "\u001b[37;1m";
    public const string Pink = "\u001b[31m";
}

public static class cc
{
    // Not sure what in gonna do with these colors
    //public readonly Spectre.Console.Color Oxford = new Spectre.Console.Color(10, 17, 40);
    //public readonly Spectre.Console.Color Penn = new Spectre.Console.Color(0, 31, 84);
    //public readonly Spectre.Console.Color Indogo = new Spectre.Console.Color(3, 64, 120);
    //public readonly Spectre.Console.Color Cerolean = new Spectre.Console.Color(18, 130, 162);
    //public readonly Spectre.Console.Color White = new Spectre.Console.Color(254, 252, 251);
    // Custom color pallete
    public const string violet = "#2D4CB4";
    public const string neon = "#1F71FF";
    public const string bleu = "#1388F6";
    public const string vsky = "#5ACDED";
    public const string ceru = "#107793";
    public const string white = "#FEFCFB";
}
