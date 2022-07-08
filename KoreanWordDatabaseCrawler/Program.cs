using System.CommandLine;
using System.Xml;
using Spectre.Console;

var apiKeyArgument = new Argument<string>(
    "API Key",
    description: "Personal API key");

var fileOption = new Option<FileInfo>(
    "--file",
    getDefaultValue: () => new FileInfo("./result.txt"),
    description: "File path where the results will be stored"
);

var threadOption = new Option<int>(
    "--threads",
    getDefaultValue: () => 16,
    description: "Numbers of thread");

var maxIdOption = new Option<int>(
    "--maxid",
    getDefaultValue: () => 70000,
    description: "Max Id");
        
var rootCommand = new RootCommand
{
    apiKeyArgument,
    fileOption,
    threadOption,
    maxIdOption
};

rootCommand.Description = "Gets the word from the National Institute of Korean Language API.";

rootCommand.SetHandler(async (apiKey, file, threadCount, maxId) =>
{
    var words = new List<string>();

    void PrintLog(Color color, Color messageColor, string taskName, string taskMessage, int threadId)
    {
        AnsiConsole.MarkupLine("[bold {0}][[{1} - #{2}]][/] [{3}]{4}[/]",  color, taskName, threadId, messageColor, taskMessage);
    }

    async Task<string> DownloadString(string url)
    {
        PrintLog(Color.DeepSkyBlue4_1, Color.White,
            taskName: "DownloadString", 
            taskMessage: "Started!", Environment.CurrentManagedThreadId
        );

        string result = string.Empty;
        
        try
        {
            var client = new HttpClient();
            var httpResponse = await client.GetAsync(url);
            
            result = httpResponse is {IsSuccessStatusCode: true} 
                ? httpResponse.Content.ReadAsStringAsync().Result : string.Empty;
        }
        catch (Exception ex)
        {
            PrintLog(Color.DeepSkyBlue4_1, Color.Red,
                taskName: "DownloadString", 
                taskMessage: ex.Message, Environment.CurrentManagedThreadId
            );
        }
        
        PrintLog(Color.DeepSkyBlue4_1, Color.Green,
            taskName: "DownloadString", 
            taskMessage: "Done!", Environment.CurrentManagedThreadId
        );
        
        return result;
    }

    async Task<string> GetWord(int wordId)
    {
        PrintLog(Color.DeepSkyBlue1, Color.White,
            taskName: "GetWord", 
            taskMessage: "Started!", Environment.CurrentManagedThreadId
        );

        string response = await DownloadString($"https://stdict.korean.go.kr/api/view.do?key={apiKey}&method=target_code&q={wordId}");

        var xml = new XmlDocument();

        try
        {
            xml.LoadXml(response);
        }
        catch (Exception ex)
        {
            PrintLog(Color.DeepSkyBlue1, Color.Red,
                taskName: "GetWord", 
                taskMessage: ex.Message, Environment.CurrentManagedThreadId
            );
        }

        var node = xml.GetElementsByTagName("word")[0];
        
        string word = node != null
            ? node.InnerText.Replace("^", string.Empty).Replace("-", string.Empty) : string.Empty;

        PrintLog(Color.DeepSkyBlue1, Color.Green,
            taskName: "GetWord", 
            taskMessage: $"Done! / {word}", Environment.CurrentManagedThreadId
        );

        return word;
    }

    async Task GetWordWithRange(int start, int end)
    {
        PrintLog(Color.Cyan1, Color.White,
            taskName: "GetWordWithRange", 
            taskMessage: "Started!", Environment.CurrentManagedThreadId
        );

        for (int i = start; i < end; i++)
        {
            string word = await GetWord(i);

            if (words.Contains(word))
                continue;
            
            words.Add(word);
        }
        
        PrintLog(Color.Cyan1, Color.Green,
            taskName: "GetWordWithRange", 
            taskMessage: "Done!", Environment.CurrentManagedThreadId
        );
    }

    var client = new HttpClient();
    var font = FigletFont.Parse(await client.GetStringAsync("http://www.figlet.org/fonts/lean.flf"));
    
    AnsiConsole.Write(
        new FigletText(font, "Korean Word Database Crawler")
            .LeftAligned()
            .Color(Color.LightSlateBlue));

    Console.Write("Press enter key to continue...");
    Console.ReadLine();
    
    var rule = new Rule("[white]Log[/]")
    {
        Alignment = Justify.Left
    };
    AnsiConsole.Write(rule);

    var tasks = new List<Task>();
    var dictionary = new Dictionary<int, int>();
    
    int step = maxId / threadCount;

    for (int i = 1; i < threadCount + 1; i++)
    {
        if (i != threadCount)
            dictionary.Add(step * (i - 1) + 1, step * i);
        else
            dictionary.Add(step * (i - 1) + 1, maxId);
    }

    foreach (var item in dictionary)
    {
        var task = GetWordWithRange(item.Key, item.Value);
        tasks.Add(task);
    }

    Task.WaitAll(tasks.ToArray());
    tasks.Clear();

    foreach (var word in words)
    {
        File.AppendAllText(file.FullName, word + Environment.NewLine);
    }
}, apiKeyArgument, fileOption, threadOption, maxIdOption);

return rootCommand.Invoke(args);