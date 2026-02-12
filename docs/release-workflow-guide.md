# Release Workflow Guide

## Overview

This automated release workflow handles versioning, Docker image builds/pushes, and GitHub releases with minimal manual intervention.

## Features

✅ **Manual Version Control** - Specify version numbers when running the workflow  
✅ **Release Type Options** - Choose between `stable`, `alpha`, or `beta`  
✅ **Branch Protection** - Alpha/beta releases only allowed from main branch  
✅ **Automated Docker Builds** - Multi-platform builds with cache  
✅ **Git Tagging** - Automatic semantic versioning tags (v1.0.0, v1.0.0-alpha, etc.)  
✅ **GitHub Releases** - Auto-generated release notes with changelog  
✅ **Pre-release Support** - Alpha/beta marked as pre-releases on GitHub  

## How to Trigger a Release

1. Go to your GitHub repository
2. Navigate to **Actions** → **Release** workflow
3. Click **Run workflow**
4. Fill in the inputs:
   - **Version**: Enter semantic version (e.g., `1.0.0`)
   - **Release type**: Choose `stable`, `alpha`, or `beta`
   - **Create release**: Check to auto-create GitHub Release (default: true)
5. Click **Run workflow**

### Examples

#### Stable Release
```
Version: 1.2.3
Release type: stable
```
Creates: `v1.2.3` tag + `clamav-api:1.2.3` + `clamav-api:latest` Docker images

#### Alpha Release
```
Version: 1.2.3
Release type: alpha
```
Creates: `v1.2.3-alpha` tag + `clamav-api:1.2.3-alpha` Docker image  
*Only works from main branch*

#### Beta Release
```
Version: 1.2.3
Release type: beta
```
Creates: `v1.2.3-beta` tag + `clamav-api:1.2.3-beta` Docker image  
*Only works from main branch*

## Repository Secrets Setup

Configure these secrets in your GitHub repository settings (**Settings** → **Secrets and variables** → **Actions**):

### Required Secrets

| Secret | Description | Example |
|--------|-------------|---------|
| `DOCKER_REGISTRY_USERNAME` | Docker Hub username | `myusername` |
| `DOCKER_REGISTRY_PASSWORD` | Docker Hub token/password | (Personal access token) |

### How to Create Docker Hub Token

1. Go to [Docker Hub](https://hub.docker.com)
2. Login to your account
3. Go to **Account Settings** → **Security**
4. Click **New Access Token**
5. Create with read/write access
6. Copy token to GitHub secret `DOCKER_REGISTRY_PASSWORD`

## Release Rules

### ✅ Allowed Operations

| Scenario | Main | Dev | Feature | Status |
|----------|------|-----|---------|--------|
| Stable release | ✅ Yes | ❌ No | ❌ No | Full release |
| Alpha release | ✅ Yes | ❌ No | ❌ No | Pre-release |
| Beta release | ✅ Yes | ❌ No | ❌ No | Pre-release |

### Validation Rules

1. **Version Format**: Must be valid semver (e.g., `1.0.0`)
   - ❌ Invalid: `1.0`, `v1.0.0`, `latest`
   - ✅ Valid: `1.0.0`, `2.3.4`, `0.0.1`

2. **Branch Restrictions**:
   - `stable` releases: Any branch
   - `alpha`/`beta` releases: **main branch only**

3. **Version Uniqueness**: You can re-release the same version to iterate on alpha/beta

## Workflow Steps

The release workflow performs these automated steps:

1. **Validate** - Checks version format and branch rules
2. **Build Docker Image** - Multi-stage build with cache optimization
3. **Push to Docker Hub** - Tags appropriately (latest for stable)
4. **Create Git Tag** - Semantic version tag (e.g., v1.2.3)
5. **Generate Release Notes** - Includes commit history and pull request info
6. **Create GitHub Release** - With auto-generated notes and asset tracking

## Docker Image Tagging

### Stable Release (v1.2.3)
```bash
docker pull myusername/clamav-api:1.2.3    # Specific version
docker pull myusername/clamav-api:latest   # Latest stable
```

### Pre-release (v1.2.3-alpha)
```bash
docker pull myusername/clamav-api:1.2.3-alpha   # Specific alpha version
```

### Pre-release (v1.2.3-beta)
```bash
docker pull myusername/clamav-api:1.2.3-beta    # Specific beta version
```

## Using Released Containers

### Pull and Run Stable
```bash
docker pull myusername/clamav-api:1.2.3
docker run -p 8080:8080 myusername/clamav-api:1.2.3
```

### Pull and Run Latest Stable
```bash
docker pull myusername/clamav-api:latest
docker run -p 8080:8080 myusername/clamav-api:latest
```

### Pull and Run Pre-release
```bash
docker pull myusername/clamav-api:1.2.3-alpha
docker run -p 8080:8080 myusername/clamav-api:1.2.3-alpha
```

## What Gets Created in Each Release

### Git Tags
```
v1.0.0-alpha    # Alpha pre-release tag
v1.0.0-beta     # Beta pre-release tag
v1.0.0          # Stable release tag
```

### GitHub Release
- Tag name: `v1.0.0`
- Release notes with:
  - Docker pull command
  - Commit history since last release
  - Build timestamp
  - Links to commits

### Docker Images
Tagged on Docker Hub and ready to pull

## Release Notes Contents

Auto-generated release notes include:

- 📌 Release type (stable/alpha/beta) with warning for pre-releases
- 🐳 Docker pull command
- 📝 Commit log since last release (author, hash, message)
- ⏰ Build timestamp
- 🔗 Direct commit links

## Troubleshooting

### "Version format invalid"
**Issue**: Entered version doesn't match semver  
**Solution**: Use format `MAJOR.MINOR.PATCH` (e.g., `1.0.0`)

### "Alpha/beta only from main branch"
**Issue**: Tried alpha/beta release from feature branch  
**Solution**: Switch to main branch or use stable release

### "Docker push failed"
**Issue**: Secret credentials not set or invalid  
**Solution**: Check `DOCKER_REGISTRY_USERNAME` and `DOCKER_REGISTRY_PASSWORD` in GitHub Secrets

### "Git tag already exists"
**Issue**: Tried to release same version twice  
**Solution**: Increment version number or use different release type (e.g., 1.0.0-alpha after 1.0.0)

## Migration Notes from GitVersion

Unlike GitVersion which auto-calculates versions, this workflow:
- ✅ Gives you **full control** over version numbers
- ✅ Supports **manual versioning** strategy
- ✅ Works with **simple semver** (no complex git workflows needed)
- ✅ Allows **immediate re-release** for quick fixes to pre-releases

## Future Enhancements

To add ACR support later, these lines can be updated:

```yaml
# In the login step, add:
- name: Login to ACR
  uses: docker/login-action@v3
  with:
    registry: ${{ secrets.ACR_REGISTRY_URL }}
    username: ${{ secrets.ACR_USERNAME }}
    password: ${{ secrets.ACR_PASSWORD }}

# In build-push, add ACR tags to IMAGE_TAGS
```

## GitHub Release Page

Each release generates a professional release page showing:
- Release title and version
- Full changelog
- Docker pull instructions
- Commit metadata
- Prerelease badge (for alpha/beta)

You can view releases at: `https://github.com/yourusername/yourrepo/releases`
