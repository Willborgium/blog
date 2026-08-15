# Blog

This repository deploys as a static site generated from the markdown content in `raw/`.

## Local generation

Run the generator from the repo root:

```bash
dotnet run --project Blog/Blog.Generator/Blog.Generator.csproj --configuration Release
```

This creates a static site under `site/` with:

- `site/index.html`
- `site/blog/*.html`
- `site/css/main.css`
- `site/favicon.ico`

## Commit and deploy flow

1. Update content in `raw/`.
2. Run the generator.
3. Commit the updated content plus the generated `site/` files.
4. Push to `main`.
5. Cloudflare Pages deploys the committed static output.

## Cloudflare Pages settings

For the `blog` project in Cloudflare Pages:

- Production branch: `main`
- Framework preset: `None`
- Root directory: `site`
- Build command: `exit 0`
- Build output directory: `.`

This lets Cloudflare serve the generated static folder without trying to build the .NET project again.

## GitHub workflow note

The GitHub Actions workflow in `.github/workflows/main_wjcustode-blog.yml` is manual-only validation. Production deployment should come from Cloudflare Pages Git integration, not from GitHub Actions.
