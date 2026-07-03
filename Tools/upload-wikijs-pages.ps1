# Uploads docs/wiki markdown pages to Wiki.js via GraphQL API.
# Usage:
#   $env:WIKIJS_API_TOKEN = "your-token"
#   pwsh Tools/upload-wikijs-pages.ps1
#
# Optional:
#   $env:WIKIJS_BASE_URL = "https://backmen.ru"

param(
    [string]$BaseUrl = $(if ($env:WIKIJS_BASE_URL) { $env:WIKIJS_BASE_URL } else { "https://backmen.ru" }),
    [string]$Token = $env:WIKIJS_API_TOKEN,
    [string]$Locale = "ru",
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

if (-not $Token) {
    Write-Error "Set WIKIJS_API_TOKEN environment variable."
}

$pages = @(
    @{
        Path        = "ss14"
        Title       = "Space Station 14"
        Description = "Главная страница вики BACKMEN SS14"
        Tags        = @("ss14", "backmen")
        Editor      = "code"
        File        = Join-Path $RepoRoot "docs/wiki/ss14/index.html"
    },
    @{
        Path        = "ss14/rules"
        Title       = "Правила сервера"
        Description = "Правила BACKMEN из игрового Guidebook"
        Tags        = @("ss14", "rules", "backmen")
        File        = Join-Path $RepoRoot "docs/wiki/ss14/rules.md"
    },
    @{
        Path        = "ss14/contraband"
        Title       = "Контрабанда"
        Description = "Перечень контрабандных предметов из прототипов игры"
        Tags        = @("ss14", "security", "contraband")
        File        = Join-Path $RepoRoot "docs/wiki/ss14/contraband.md"
    },
    @{
        Path        = "ss14/dev"
        Title       = "Разработка BACKMEN"
        Description = "Гайды по уникальным механикам форка Backmen и модификации прототипов"
        Tags        = @("dev", "backmen", "ss14")
        File        = Join-Path $RepoRoot "docs/wiki/ss14/dev/index.md"
    },
    @{
        Path        = "ss14/dev/wounds"
        Title       = "Раны (Wounds)"
        Description = "Система ран Backmen: wounds.yml, типы урона, Pain/Bleed/Trauma"
        Tags        = @("dev", "backmen", "wounds", "medical")
        File        = Join-Path $RepoRoot "docs/wiki/ss14/dev/wounds.md"
    }
)

function Invoke-WikiGraphQL {
    param(
        [string]$Query,
        [hashtable]$Variables = @{}
    )

    $body = @{ query = $Query; variables = $Variables } | ConvertTo-Json -Depth 20 -Compress
    $response = Invoke-RestMethod `
        -Uri "$BaseUrl/graphql" `
        -Method Post `
        -Headers @{
            Authorization  = "Bearer $Token"
            "Content-Type" = "application/json"
        } `
        -Body ([System.Text.Encoding]::UTF8.GetBytes($body))

    if ($response.errors) {
        throw ($response.errors | ConvertTo-Json -Depth 10)
    }

    return $response.data
}

function Get-WikiPageId {
    param([string]$Path)

    $query = @'
query ($locale: String!) {
  pages {
    list(locale: $locale) {
      id
      path
      title
    }
  }
}
'@

    $data = Invoke-WikiGraphQL -Query $query -Variables @{ locale = $Locale }
    return $data.pages.list | Where-Object { $_.path -eq $Path } | Select-Object -First 1
}

function New-WikiPage {
    param(
        [string]$Path,
        [string]$Title,
        [string]$Description,
        [string]$Content,
        [string[]]$Tags,
        [string]$Editor = "markdown"
    )

    $mutation = @'
mutation (
  $content: String!
  $description: String!
  $editor: String!
  $isPublished: Boolean!
  $isPrivate: Boolean!
  $locale: String!
  $path: String!
  $tags: [String]!
  $title: String!
) {
  pages {
    create(
      content: $content
      description: $description
      editor: $editor
      isPublished: $isPublished
      isPrivate: $isPrivate
      locale: $locale
      path: $path
      tags: $tags
      title: $title
    ) {
      responseResult {
        succeeded
        message
        errorCode
      }
      page {
        id
        path
        title
      }
    }
  }
}
'@

    $variables = @{
        content     = $Content
        description = $Description
        editor      = $Editor
        isPublished = $true
        isPrivate   = $false
        locale      = $Locale
        path        = $Path
        tags        = $Tags
        title       = $Title
    }

    return Invoke-WikiGraphQL -Query $mutation -Variables $variables
}

function Update-WikiPage {
    param(
        [int]$Id,
        [string]$Path,
        [string]$Title,
        [string]$Description,
        [string]$Content,
        [string[]]$Tags,
        [string]$Editor = "markdown"
    )

    $mutation = @'
mutation (
  $id: Int!
  $content: String!
  $description: String!
  $editor: String!
  $isPublished: Boolean!
  $isPrivate: Boolean!
  $locale: String!
  $path: String!
  $tags: [String]!
  $title: String!
) {
  pages {
    update(
      id: $id
      content: $content
      description: $description
      editor: $editor
      isPublished: $isPublished
      isPrivate: $isPrivate
      locale: $locale
      path: $path
      tags: $tags
      title: $title
    ) {
      responseResult {
        succeeded
        message
        errorCode
      }
      page {
        id
        path
        title
      }
    }
  }
}
'@

    $variables = @{
        id          = $Id
        content     = $Content
        description = $Description
        editor      = $Editor
        isPublished = $true
        isPrivate   = $false
        locale      = $Locale
        path        = $Path
        tags        = $Tags
        title       = $Title
    }

    return Invoke-WikiGraphQL -Query $mutation -Variables $variables
}

foreach ($page in $pages) {
    $editor = if ($page.Editor) { $page.Editor } else { "markdown" }
    if (-not (Test-Path $page.File)) {
        throw "Missing file: $($page.File)"
    }

    $content = Get-Content -Path $page.File -Raw -Encoding UTF8
    $existing = Get-WikiPageId -Path $page.Path

    if ($existing) {
        Write-Host "Updating $($page.Path) (id=$($existing.id))..."
        $result = Update-WikiPage `
            -Id $existing.id `
            -Path $page.Path `
            -Title $page.Title `
            -Description $page.Description `
            -Content $content `
            -Tags $page.Tags `
            -Editor $editor
        $response = $result.pages.update.responseResult
    }
    else {
        Write-Host "Creating $($page.Path)..."
        $result = New-WikiPage `
            -Path $page.Path `
            -Title $page.Title `
            -Description $page.Description `
            -Content $content `
            -Tags $page.Tags `
            -Editor $editor
        $response = $result.pages.create.responseResult
    }

    if (-not $response.succeeded) {
        throw "Failed for $($page.Path): $($response.message) ($($response.errorCode))"
    }

    Write-Host "OK: $($page.Path) -> $BaseUrl/$Locale/$($page.Path)"
}

Write-Host "Done."

# Optional: upload contraband icons (requires write:assets on API token).
if ($env:WIKIJS_UPLOAD_ICONS -eq "1") {
    Write-Host "Uploading contraband icons..."
    python (Join-Path $PSScriptRoot "upload_wiki_icons.py")
    if ($LASTEXITCODE -ne 0) { throw "Icon upload failed." }
}
