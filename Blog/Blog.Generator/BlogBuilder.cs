using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blog.Generator
{
    public class BlogBuilder
    {
        private const string TemplatePath = "blog-post-template.html";
        private const string InputDirectory = "c:/source/blog/raw";
        private const string OutputDirectory = "c:/source/blog/Blog/Blog.Web/wwwroot/blog";

        public async Task GenerateArticlesAsync()
        {
            foreach (var rawFilePath in Directory.EnumerateFiles(InputDirectory))
            {
                await GenerateFileAsync(rawFilePath);
            }
        }

        private async Task GenerateFileAsync(string inputPath)
        {
            Console.WriteLine($">> Reading {inputPath}");

            string content = null;

            using (var reader = new StreamReader(inputPath))
            {
                content = await reader.ReadToEndAsync();
            }

            var lines = content.Split(Environment.NewLine);

            var output = new StringBuilder();

            var hasTitle = false;
            var hasDate = false;
            var isInCodeBlock = false;
            var isInList = false;
            var isOrderedList = false;

            foreach (var line in lines)
            {
                if (line.StartsWith('-') || line.StartsWith('#'))
                {
                    if (!isInList)
                    {
                        isInList = true;

                        if (line.StartsWith('-'))
                        {
                            output.AppendLine("<ul>");
                        }
                        else
                        {
                            output.AppendLine("<ol>");
                        }
                    }
                }
                else if (isInList)
                {
                    isInList = false;

                    if (isOrderedList)
                    {
                        output.AppendLine("</ol>");
                    }
                    else
                    {
                        output.AppendLine("</ul>");
                    }
                }
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
                else if (line.Trim().StartsWith("///code"))
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
                else if (line.StartsWith('-') || line.StartsWith('#'))
                {
                    output.AppendLine($"<li>{line.AsSpan(2)}</li>");
                }
                else
                {
                    output.AppendLine($"<p class=\"text-justify\">{line}</p>");
                }
            }

            var outputName = Path.GetFileNameWithoutExtension(inputPath);
            var outputPath = Path.Combine(OutputDirectory, $"{outputName}.html");

            Console.WriteLine($">> Writing {outputPath}");

            var template = await GetTemplateAsync();

            using (var writer = new StreamWriter(outputPath))
            {
                await writer.WriteAsync(string.Format(template, output));
            }
        }

        private async Task<string> GetTemplateAsync()
        {
            if (_template != null)
            {
                return _template;
            }

            using (var reader = new StreamReader(TemplatePath))
            {
                return _template = await reader.ReadToEndAsync();
            }
        }

        private string _template;
    }
}
