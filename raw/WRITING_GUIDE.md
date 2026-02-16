# Writing posts in plain text

You can keep writing posts with the current simple format:

1. First non-empty line: title
2. Second non-empty line: date (recommended `yyyy-MM-dd`)
3. Everything else: body

## Optional metadata format

If you want explicit fields, you can use this at the top:

Title: My Post Title
Date: 2026-02-16
Summary: One short sentence shown on the home page.
Slug: my-post-title

Then add a blank line, then body content.

## Body syntax

Body is rendered as Markdown, with a few compatibility helpers:

- `///code#csharp` and `///code` are supported (legacy style)
- Standard Markdown code fences also work (` ```csharp ` ... ` ``` `)
- `$youtube VIDEO_ID` embeds a responsive YouTube player
- Inline HTML is still allowed when needed

## Tips for best output

- Use a short summary in metadata for better home-page previews.
- Use `#`, `##`, `###` headings in Markdown for structure.
- Keep paragraphs short for mobile readability.
- Prefer ISO dates (`yyyy-MM-dd`) for consistent sorting.
