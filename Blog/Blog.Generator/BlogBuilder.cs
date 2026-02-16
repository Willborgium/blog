using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Markdig;

namespace Blog.Generator
{
    public class BlogBuilder
    {
        private readonly MarkdownPipeline _markdown = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public async Task GenerateSiteAsync(string[] args)
        {
            var paths = ResolvePaths(args);

            Directory.CreateDirectory(paths.OutputDirectory);

            var posts = new List<BlogPost>();

            foreach (var rawFilePath in Directory.EnumerateFiles(paths.InputDirectory, "*.txt").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($">> Reading {rawFilePath}");
                posts.Add(await ParsePostAsync(rawFilePath));
            }

            posts = posts
                .OrderByDescending(p => p.DateSort)
                .ThenByDescending(p => p.Slug, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var postTemplate = await GetTemplateAsync(paths.PostTemplatePath);

            foreach (var post in posts)
            {
                var outputPath = Path.Combine(paths.OutputDirectory, $"{post.Slug}.html");
                Console.WriteLine($">> Writing {outputPath}");

                var output = string.Format(
                    postTemplate,
                    WebUtility.HtmlEncode(post.Title),
                    WebUtility.HtmlEncode(post.Description),
                    WebUtility.HtmlEncode(post.DateMachine),
                    WebUtility.HtmlEncode(post.DateDisplay),
                    post.HtmlBody);

                await File.WriteAllTextAsync(outputPath, output);
            }

            var indexTemplate = await GetTemplateAsync(paths.IndexTemplatePath);
            var postListMarkup = string.Join(Environment.NewLine, posts.Select(RenderPostListItem));
            var indexOutput = string.Format(indexTemplate, postListMarkup);

            Console.WriteLine($">> Writing {paths.IndexOutputPath}");
            await File.WriteAllTextAsync(paths.IndexOutputPath, indexOutput);
        }

        private async Task<BlogPost> ParsePostAsync(string inputPath)
        {
            var content = await File.ReadAllTextAsync(inputPath);
            var lines = SplitLines(content);
            var slugFromFile = Path.GetFileNameWithoutExtension(inputPath);

            var cursor = 0;
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ParseMetadata(lines, metadata, ref cursor);

            string title;
            string dateRaw;

            if (metadata.Count == 0)
            {
                title = ReadNextNonEmptyLine(lines, ref cursor);
                dateRaw = ReadNextNonEmptyLine(lines, ref cursor);
            }
            else
            {
                title = GetMetadataValue(metadata, "title");
                dateRaw = GetMetadataValue(metadata, "date");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new InvalidOperationException($"Missing title in post '{inputPath}'.");
            }

            if (string.IsNullOrWhiteSpace(dateRaw))
            {
                throw new InvalidOperationException($"Missing date in post '{inputPath}'.");
            }

            while (cursor < lines.Length && string.IsNullOrWhiteSpace(lines[cursor]))
            {
                cursor++;
            }

            var bodyLines = lines.Skip(cursor).ToArray();
            var bodyMarkdown = NormalizeBody(bodyLines);
            var bodyHtml = Markdown.ToHtml(bodyMarkdown, _markdown);

            var slug = NormalizeSlug(GetMetadataValue(metadata, "slug"), slugFromFile);
            var summary = GetMetadataValue(metadata, "summary");

            if (string.IsNullOrWhiteSpace(summary))
            {
                summary = BuildSummary(bodyLines, title);
            }

            var parsedDate = ParseDate(dateRaw);

            return new BlogPost(
                Slug: slug,
                Title: title.Trim(),
                Description: summary,
                DateDisplay: parsedDate?.ToString("yyyy-MM-dd") ?? dateRaw.Trim(),
                DateMachine: parsedDate?.ToString("yyyy-MM-dd") ?? dateRaw.Trim(),
                DateSort: parsedDate ?? DateTime.MinValue,
                HtmlBody: bodyHtml);
        }

        private static string[] SplitLines(string content)
        {
            return content.Replace("\r\n", "\n").Split('\n');
        }

        private static void ParseMetadata(string[] lines, IDictionary<string, string> metadata, ref int cursor)
        {
            while (cursor < lines.Length)
            {
                var line = lines[cursor];

                if (string.IsNullOrWhiteSpace(line))
                {
                    cursor++;
                    break;
                }

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    metadata.Clear();
                    cursor = 0;
                    return;
                }

                var key = line.Substring(0, separatorIndex).Trim();
                var value = line.Substring(separatorIndex + 1).Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    metadata.Clear();
                    cursor = 0;
                    return;
                }

                metadata[key] = value;
                cursor++;
            }
        }

        private static string ReadNextNonEmptyLine(string[] lines, ref int cursor)
        {
            while (cursor < lines.Length)
            {
                var line = lines[cursor++].Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            return string.Empty;
        }

        private static string NormalizeBody(IEnumerable<string> lines)
        {
            var normalizedInput = lines.Select(NormalizeLine).ToList();
            var output = new List<string>();

            var inCodeBlock = false;
            var inList = false;

            for (var i = 0; i < normalizedInput.Count; i++)
            {
                var line = normalizedInput[i];
                var trimmed = line.Trim();

                if (trimmed.StartsWith("```"))
                {
                    output.Add(line);
                    inCodeBlock = !inCodeBlock;
                    continue;
                }

                if (inCodeBlock)
                {
                    output.Add(line);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    if (output.Count == 0 || !string.IsNullOrWhiteSpace(output[^1]))
                    {
                        output.Add(string.Empty);
                    }

                    inList = false;
                    continue;
                }

                var isListItem = IsListLine(trimmed);

                if (inList && !isListItem && (output.Count == 0 || !string.IsNullOrWhiteSpace(output[^1])))
                {
                    output.Add(string.Empty);
                }

                if (!inList && isListItem && output.Count > 0 && !string.IsNullOrWhiteSpace(output[^1]))
                {
                    output.Add(string.Empty);
                }

                output.Add(line);

                if (isListItem)
                {
                    inList = true;
                    continue;
                }

                inList = false;

                var nextNonEmpty = normalizedInput
                    .Skip(i + 1)
                    .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));

                if (!string.IsNullOrWhiteSpace(nextNonEmpty)
                    && !nextNonEmpty.Trim().StartsWith("```")
                    && !IsListLine(nextNonEmpty.Trim()))
                {
                    output.Add(string.Empty);
                }
            }

            return string.Join(Environment.NewLine, output);
        }

        private static bool IsListLine(string line)
        {
            return line.StartsWith("- ") || Regex.IsMatch(line, "^\\d+\\.\\s+");
        }

        private static string NormalizeLine(string line)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("///code", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.Equals("///code", StringComparison.OrdinalIgnoreCase))
                {
                    return "```";
                }

                var separator = trimmed.IndexOf('#');
                if (separator > 0 && separator < trimmed.Length - 1)
                {
                    return "```" + trimmed.Substring(separator + 1).Trim();
                }

                return "```";
            }

            if (trimmed.StartsWith("$youtube ", StringComparison.OrdinalIgnoreCase))
            {
                var videoId = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (!string.IsNullOrWhiteSpace(videoId) && Regex.IsMatch(videoId, "^[A-Za-z0-9_-]{11}$"))
                {
                    return $"<div class=\"video-embed\"><iframe src=\"https://www.youtube.com/embed/{videoId}\" title=\"YouTube video\" loading=\"lazy\" frameborder=\"0\" allow=\"accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share\" referrerpolicy=\"strict-origin-when-cross-origin\" allowfullscreen></iframe></div>";
                }
            }

            return line;
        }

        private static string BuildSummary(IEnumerable<string> bodyLines, string fallback)
        {
            var summary = bodyLines
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Where(l => !l.StartsWith("///code", StringComparison.OrdinalIgnoreCase))
                .Where(l => !l.StartsWith("$youtube ", StringComparison.OrdinalIgnoreCase))
                .Where(l => !l.StartsWith("- "))
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(summary))
            {
                return fallback;
            }

            summary = Regex.Replace(summary, "<.*?>", string.Empty).Trim();

            if (summary.Length > 170)
            {
                summary = summary.Substring(0, 167).TrimEnd() + "...";
            }

            return string.IsNullOrWhiteSpace(summary) ? fallback : summary;
        }

        private static string RenderPostListItem(BlogPost post)
        {
            return $@"                <a href=""blog/{WebUtility.HtmlEncode(post.Slug)}.html"" class=""list-group-item list-group-item-action"">
                    <div class=""post-list-row"">
                        <h2 class=""post-title"">{WebUtility.HtmlEncode(post.Title)}</h2>
                        <time class=""post-date"" datetime=""{WebUtility.HtmlEncode(post.DateMachine)}"">{WebUtility.HtmlEncode(post.DateDisplay)}</time>
                    </div>
                    <p class=""post-subtitle text-muted"">{WebUtility.HtmlEncode(post.Description)}</p>
                </a>";
        }

        private static DateTime? ParseDate(string raw)
        {
            if (DateTime.TryParse(raw, out var parsed))
            {
                return parsed.Date;
            }

            return null;
        }

        private static string GetMetadataValue(IDictionary<string, string> metadata, string key)
        {
            return metadata.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private static string NormalizeSlug(string preferredSlug, string fallbackSlug)
        {
            var input = string.IsNullOrWhiteSpace(preferredSlug) ? fallbackSlug : preferredSlug;
            var lowered = input.Trim().ToLowerInvariant();
            var slug = Regex.Replace(lowered, "[^a-z0-9\\-\\s]", string.Empty);
            slug = Regex.Replace(slug, "\\s+", "-");
            slug = Regex.Replace(slug, "-+", "-").Trim('-');

            return string.IsNullOrWhiteSpace(slug) ? fallbackSlug.ToLowerInvariant() : slug;
        }

        private static Paths ResolvePaths(string[] args)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var solutionDirectory = FindDirectoryContaining(currentDirectory, "Blog.sln")
                ?? FindDirectoryContaining(Path.Combine(currentDirectory, "Blog"), "Blog.sln")
                ?? throw new DirectoryNotFoundException("Could not locate Blog.sln from current directory.");

            var repositoryRoot = Directory.GetParent(solutionDirectory)?.FullName
                ?? throw new DirectoryNotFoundException("Could not determine repository root directory.");

            var inputDirectory = args?.Length > 0
                ? args[0]
                : Path.Combine(repositoryRoot, "raw");

            return new Paths(
                InputDirectory: inputDirectory,
                OutputDirectory: Path.Combine(solutionDirectory, "Blog.Web", "wwwroot", "blog"),
                PostTemplatePath: Path.Combine(AppContext.BaseDirectory, "blog-post-template.html"),
                IndexTemplatePath: Path.Combine(AppContext.BaseDirectory, "blog-index-template.html"),
                IndexOutputPath: Path.Combine(solutionDirectory, "Blog.Web", "wwwroot", "index.html"));
        }

        private static string FindDirectoryContaining(string startDirectory, string targetFile)
        {
            var cursor = new DirectoryInfo(startDirectory);

            while (cursor != null)
            {
                var candidate = Path.Combine(cursor.FullName, targetFile);
                if (File.Exists(candidate))
                {
                    return cursor.FullName;
                }

                cursor = cursor.Parent;
            }

            return null;
        }

        private static async Task<string> GetTemplateAsync(string path)
        {
            return await File.ReadAllTextAsync(path);
        }

        private record BlogPost(
            string Slug,
            string Title,
            string Description,
            string DateDisplay,
            string DateMachine,
            DateTime DateSort,
            string HtmlBody);

        private record Paths(
            string InputDirectory,
            string OutputDirectory,
            string PostTemplatePath,
            string IndexTemplatePath,
            string IndexOutputPath);
    }
}
