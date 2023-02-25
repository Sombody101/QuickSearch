using Spectre.Console;
using System.Diagnostics;
using System.Security.AccessControl;
using YamlDotNet.Serialization;

namespace QuickSearch;

#pragma warning disable IDE1006
public class Program
{
    public static void Main(string[] args)
    {
        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()))
        {
            log("This app will only work with Windows or Linux", 3);
            return;
        }

        if (args.Length > 0 && System.IO.Path.GetExtension(args[0]).ToLower() == ".sh")
        {
            log("This app has not been tested to work in scripts. Be advised.", 2);
            var l = args.ToList();
            l.Remove(args[0]);
            args = l.ToArray();
        }

        if (args.Length is 0)
        {
            log("No arguments provided", 3);
            return;
        }

        // Setup Args
        Args inpArgs;
        try
        {
            ReadSettings(out inpArgs);
            if (inpArgs is null)
            {
                log("Settigns file is empty. Proceeding with default values", 2);
                inpArgs = new();
            }
        }
        catch (FileNotFoundException)
        {
            log($"Could not find '{InAppArgs.PathToSettings()}'. Proceeding with default values", 2);
            inpArgs = new();
        }
        catch (Exception ex)
        {
            log($"Unknown error while loading 'qs-settings.yaml'\r\nError message: {ex.Message}", 3);
            return;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.Contains(':'))
            {
                // Sets the settins in the .yaml file
                TrySep(arg, out string setting, out string value);
                switch (setting)
                {
                    case "--pref-search-engine":
                        inpArgs.PreferedSearchEngine = value;
                        args[i] = "";
                        break;
                    case "--pref-profile":
                        inpArgs.PreferedProfile = value;
                        args[i] = "";
                        break;
                    default:
                        log($"Can't parse argument '{setting}'", 3);
                        return;
                }
                WriteSettings(inpArgs);
            }
            else if (arg.StartsWith("-"))
                switch (arg)
                {
                    // Sets temporary settings for single use
                    case "-se":
                    case "--search-engine":
                        args[i] = "";
                        inpArgs.PreferedSearchEngine = args[++i];
                        args[i] = "";
                        break;
                    case "-p":
                    case "--profile":
                        args[i] = "";
                        inpArgs.PreferedProfile = args[++i];
                        args[i] = "";
                        break;
                    case "-gs":
                    case "--generate-settings":
                        WriteSettings(inpArgs);
                        args[i] = "";
                        break;
                    default:
                        log($"Can't parse argument '{arg}'", 3);
                        return;
                }
        }

        string sitelist = "";
        if (inpArgs.SpecifiedSiteList.Length is 0)
        {
            sitelist = "+";
            inpArgs.SpecifiedSiteList = InAppArgs.CodeSiteList;

            string[] tmp = new string[args.Length];
            for (int i = 0; i < tmp.Length; i++)
                if (args[i] is not "")
                {
                    tmp[i] = args[i];
                    Console.Write(tmp[i] + ": ");
                }

            args = tmp;
            if (args.Length is 0 or 1)
            {
                log("No search terms entered", 3);
                return;
            }

            foreach (string site in inpArgs.SpecifiedSiteList)
                sitelist += site + "+OR+";
            sitelist = sitelist[..^4];
        }

        string searchTerms = "";
        foreach (string arg in args)
            searchTerms += arg + "+";
        searchTerms = searchTerms[..^1];

        var searchLink = $"\"https://google.com/search?q={searchTerms}{sitelist}\"";

        if (!File.Exists(inpArgs.PreferedSearchEngine))
        {
            log($"Failed to find search engine app with path '{inpArgs.PreferedSearchEngine}'");
            return;
        }
        Action StartApp = () => Process.Start(new ProcessStartInfo()
        {
            FileName = inpArgs.PreferedSearchEngine,
            WorkingDirectory = OperatingSystem.IsLinux() ? "/" : Directory.GetCurrentDirectory(),
            Arguments = $"--profile-directory=\"{inpArgs.PreferedProfile}\" {searchLink}",
        });

        WaitFor("Starting search engine...", StartApp).GetAwaiter().GetResult();
    }

    public static string helpText =
    $@"qs []";

    public static void log(string message, byte severity = 0)
    {
        Console.WriteLine($"qs: " +
            $"{(severity is 1 ? c.White : (severity is 2 ? c.Yellow : c.Red))}" +
            $"{(severity is 1 ? "message" : (severity is 2 ? "warning" : "fatal"))}{c.norm}: " +
            $"{message}");
    }

    public static async Task WaitFor(string message, Action action)
    {
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .Start(message, ctx =>
            {
                action();
            });
    }

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
                {
                    var process = new Process();
                    process.StartInfo.FileName = "/bin/chmod";
                    process.StartInfo.Arguments = "666 " + InAppArgs.PathToSettings();
                    process.Start();
                    process.WaitForExit();
                }
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
    public static string[] CodeSiteList = {
        "site:stackoverflow.com",
        "site:reddit.com",
        "site:github.com",
    };
}

public class Args
{
    public string PreferedSearchEngine = "/mnt/c/Program Files/Google/Chrome/Application/chrome.exe";
    public string PreferedProfile = "Default";
    public string[] SpecifiedSiteList = { };
    public string[] CasualSiteList = { };
}

public static class c
{
    public const string Black = "\u001b[30m";
    public const string Red = "\u001b[38;2;255;75;75m";
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