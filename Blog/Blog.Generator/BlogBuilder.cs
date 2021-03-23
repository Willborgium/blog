using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blog.Generator
{
    public class BlogBuilder
    {
        public async Task GenerateArticles()
        {
            var rawPath = "c:/source/blog/raw";
            var outputPath = "c:/source/blog/generated";

            var rawFilePaths = Directory.EnumerateFiles(rawPath);

            foreach (var rawFilePath in rawFilePaths)
            {
                Console.WriteLine($"Building {rawFilePath}");

                string content = null;

                using (var reader = new StreamReader(rawFilePath))
                {
                    content = await reader.ReadToEndAsync();
                }

                var lines = content.Split(Environment.NewLine);

                var output = new StringBuilder();

                var hasTitle = false;
                var hasDate = false;
                var isInCodeBlock = false;

                foreach (var line in lines)
                {
                    if (!hasTitle)
                    {
                        output.AppendLine($"<h2>{line}</h2>");
                        hasTitle = true;
                    }
                    else if (!hasDate)
                    {
                        output.AppendLine($"<p class=\"font-weight-light font-italic\">{line}</p>");
                        hasDate = true;
                    }
                    else if (line.StartsWith("///code"))
                    {
                        if (isInCodeBlock)
                        {
                            var newLineLength = Environment.NewLine.Length;
                            output.Remove(output.Length - newLineLength, newLineLength);
                            output.AppendLine("</code></pre>");
                        }
                        else
                        {
                            var language = line.Split("#").Last();
                            output.Append($"<pre><code class=\"language-{language}\">");
                        }

                        isInCodeBlock = !isInCodeBlock;
                    }
                    else if (isInCodeBlock)
                    {
                        output.AppendLine(line);
                    }
                    else
                    {
                        output.AppendLine($"<p class=\"text-justify\">{line}</p>");
                    }
                }

                var outputName = Path.GetFileName(rawFilePath);

                using (var writer = new StreamWriter(Path.Combine(outputPath, outputName)))
                {
                    var format = "<div class=\"row\"><div class=\"col-2\"></div><main class=\"col-8\">{0}<div class=\"col-2\"></div></div>";
                    await writer.WriteAsync(string.Format(format, output));
                }
            }
        }
    }
}
