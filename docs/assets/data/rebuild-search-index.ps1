# Rebuilds search-index.json from all docs pages:
# - one entry per docs page
# - one entry per sidebar/nav heading (and any section/article with an id)
#
# Run from anywhere:
#   powershell -File docs/assets/data/rebuild-search-index.ps1

$ErrorActionPreference = "Stop"
$docsRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$outPath = Join-Path $PSScriptRoot "search-index.json"

function Get-HtmlText([string]$html) {
	$t = [regex]::Replace($html, "<[^>]+>", " ")
	$t = [System.Net.WebUtility]::HtmlDecode($t)
	return ($t -replace "\s+", " ").Trim()
}

function Get-DocsUrl([string]$relPath) {
	$rel = $relPath -replace "\\", "/"
	if ($rel -eq "index.html") { return "/docs/" }
	$dir = $rel -replace "/index\.html$", "/"
	if (-not $dir.StartsWith("/")) { $dir = "/$dir" }
	return "/docs$dir"
}

function Get-PageMeta([string]$html) {
	$title = ""
	$desc = ""
	if ($html -match "<title>(.*?)</title>") {
		$title = Get-HtmlText $Matches[1]
		$title = $title -replace "^OpenCAGE:\s*", ""
		if ($title -eq "OpenCAGE Documentation") { $title = "Documentation Home" }
	}
	if ($html -match 'name="description"\s+content="([^"]*)"') {
		$desc = Get-HtmlText $Matches[1]
	}
	return @{ Title = $title; Description = $desc }
}

function Get-Category([string]$url, [string]$pageTitle) {
	if ($url -eq "/docs/") { return "Home" }
	if ($url -match "^/docs/configs/") { return "Configuration" }
	if ($url -match "^/docs/cathode-entities/") { return "Cathode Entities" }
	if ($url -match "^/docs/cathode-enums/") { return "Cathode Enums" }
	if ($url -match "^/docs/behaviour-trees/") { return "Behaviour Trees" }
	if ($url -match "^/docs/changelog/") { return "Meta" }
	if ($pageTitle) { return $pageTitle }
	return "Docs"
}

function Get-SymbolDescription([string]$category, [string]$title, [string]$pageTitle) {
	if ($category -eq "Cathode Entities") {
		if ($title -like "*Interface") { return "Interface" }
		return "Entity"
	}
	if ($category -eq "Cathode Enums") { return "Enum" }
	if ($category -eq "Behaviour Trees") { return "Behaviour tree node" }
	if ($category -eq "Configuration") {
		if ($pageTitle -and $pageTitle -ne $title) { return $pageTitle }
		return "Configuration"
	}
	if ($pageTitle -and $pageTitle -ne $title) { return "On $pageTitle" }
	return "Docs section"
}

function Get-Keywords([string]$category, [string]$id, [string]$pageTitle) {
	$kw = New-Object System.Collections.Generic.List[string]
	if ($id) { $kw.Add($id) | Out-Null }
	if ($pageTitle) { $kw.Add($pageTitle.ToLowerInvariant()) | Out-Null }
	switch ($category) {
		"Cathode Entities" { $kw.Add("entity") | Out-Null; $kw.Add("interface") | Out-Null }
		"Cathode Enums" { $kw.Add("enum") | Out-Null }
		"Behaviour Trees" { $kw.Add("node") | Out-Null; $kw.Add("behaviour") | Out-Null; $kw.Add("behavior") | Out-Null }
		"Configuration" { $kw.Add("config") | Out-Null; $kw.Add("configuration") | Out-Null }
	}
	return @($kw | Select-Object -Unique)
}

$skipDirs = @("assets")
$pages = Get-ChildItem -Path $docsRoot -Recurse -Filter "index.html" | Where-Object {
	$rel = $_.FullName.Substring($docsRoot.Path.Length).TrimStart("\")
	$first = ($rel -split "\\")[0]
	$skipDirs -notcontains $first
}

$entries = New-Object System.Collections.Generic.List[object]
$seenUrls = New-Object "System.Collections.Generic.HashSet[string]"

foreach ($file in $pages) {
	$rel = $file.FullName.Substring($docsRoot.Path.Length).TrimStart("\")
	$html = Get-Content $file.FullName -Raw
	$meta = Get-PageMeta $html
	if (-not $meta.Title) { continue }

	$pageUrl = Get-DocsUrl $rel
	$category = Get-Category $pageUrl $meta.Title

	# Page-level entry
	if ($seenUrls.Add($pageUrl)) {
		$pageKeywords = @()
		if ($category -eq "Configuration") { $pageKeywords = @("config", "configuration") }
		$entries.Add([ordered]@{
			title = $meta.Title
			description = $(if ($meta.Description) { $meta.Description } else { "Documentation page" })
			url = $pageUrl
			category = $(if ($pageUrl -eq "/docs/") { "Home" } elseif ($pageUrl -match "^/docs/configs(/|$)") { "Configuration" } elseif ($pageUrl -match "changelog") { "Meta" } elseif ($pageUrl -match "cathode-|behaviour-trees") { "Reference" } else { "Getting Started" })
			keywords = $pageKeywords
			type = "page"
		}) | Out-Null
	}

	# Prefer sidebar/nav TOC links
	$navMatches = [regex]::Matches($html, '<a class="nav-link[^"]*" href="#([^"]+)">(.*?)</a>', "Singleline")
	foreach ($m in $navMatches) {
		$id = $m.Groups[1].Value.Trim()
		$title = Get-HtmlText $m.Groups[2].Value
		if (-not $id -or -not $title) { continue }
		# Skip empty icon-only leftovers
		if ($title.Length -lt 2) { continue }

		$symbolUrl = "$pageUrl#$id"
		if (-not $seenUrls.Add($symbolUrl)) { continue }

		$entries.Add([ordered]@{
			title = $title
			description = Get-SymbolDescription $category $title $meta.Title
			url = $symbolUrl
			category = $category
			keywords = Get-Keywords $category $id $meta.Title
			type = "symbol"
		}) | Out-Null
	}

	# Also pick up article/section ids with headings that may not be in the nav
	$blockMatches = [regex]::Matches(
		$html,
		'<(?:article|section)[^>]*\bid="([^"]+)"[^>]*>[\s\S]*?<(h[1-3])[^>]*class="[^"]*(?:docs-heading|section-heading)[^"]*"[^>]*>(.*?)</\2>',
		"IgnoreCase"
	)
	foreach ($m in $blockMatches) {
		$id = $m.Groups[1].Value.Trim()
		$title = Get-HtmlText $m.Groups[3].Value
		if (-not $id -or -not $title) { continue }
		$symbolUrl = "$pageUrl#$id"
		if (-not $seenUrls.Add($symbolUrl)) { continue }

		$entries.Add([ordered]@{
			title = $title
			description = Get-SymbolDescription $category $title $meta.Title
			url = $symbolUrl
			category = $category
			keywords = Get-Keywords $category $id $meta.Title
			type = "symbol"
		}) | Out-Null
	}

	# Headings that carry their own id (e.g. config h3s)
	$headingIdMatches = [regex]::Matches(
		$html,
		'<(h[1-3])[^>]*\bid="([^"]+)"[^>]*>(.*?)</\1>',
		"IgnoreCase"
	)
	foreach ($m in $headingIdMatches) {
		$id = $m.Groups[2].Value.Trim()
		$title = Get-HtmlText $m.Groups[3].Value
		if (-not $id -or -not $title) { continue }
		$symbolUrl = "$pageUrl#$id"
		if (-not $seenUrls.Add($symbolUrl)) { continue }

		$entries.Add([ordered]@{
			title = $title
			description = Get-SymbolDescription $category $title $meta.Title
			url = $symbolUrl
			category = $category
			keywords = Get-Keywords $category $id $meta.Title
			type = "symbol"
		}) | Out-Null
	}
}

# Stable sort: pages first-ish by keeping insertion order; already page-then-symbols per file
$entries | ConvertTo-Json -Depth 6 | Set-Content -Path $outPath -Encoding UTF8

$pageCount = @($entries | Where-Object { $_.type -eq "page" }).Count
$symbolCount = @($entries | Where-Object { $_.type -eq "symbol" }).Count
Write-Host "Wrote $outPath"
Write-Host "Pages: $pageCount"
Write-Host "Headings/entries: $symbolCount"
Write-Host "Total: $($entries.Count)"
Write-Host ("Size: {0:N0} KB" -f ((Get-Item $outPath).Length / 1KB))
