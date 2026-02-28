using System.Text;

namespace OlympusServiceBus.Engine.Execution.FileToApi;

internal static class CsvLineParser
{
    public static List<string> Parse(string line, char delimiter)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        
        for (var i =0; i < line.Length; i++) {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }

            if (!inQuotes && c == delimiter)
            {
                result.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            
            sb.Append(c);
        }
        
        result.Add(sb.ToString());
        return result;
    }
}