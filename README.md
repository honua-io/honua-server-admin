# Honua Server Admin

Web-based administration interface for Honua Server. Built with Blazor WebAssembly and MudBlazor.

## Overview

This is the official admin UI for managing Honua Server instances:

- **OpenRosa Form Designer**: Create and manage data collection forms
- **Layer Management**: Configure feature layers and spatial schemas
- **Service Administration**: Manage map services and API endpoints
- **Analytics Dashboard**: Monitor usage and performance

## Architecture

- **Frontend**: Blazor WebAssembly with MudBlazor components
- **Backend Communication**: Uses [honua-sdk-dotnet](https://github.com/honua-io/honua-sdk-dotnet) for gRPC client
- **Deployment**: Static web app (can be hosted on CDN)

## Development

### Prerequisites

- .NET 10.0 SDK or later
- Access to Honua Server instance

### Getting Started

```bash
# Clone repository
git clone https://github.com/honua-io/honua-server-admin.git
cd honua-server-admin

# Restore dependencies
dotnet restore

# Run development server
dotnet run --project src/Honua.Admin

# Open browser to https://localhost:5001
```

### Configuration

Configure server connection in `src/Honua.Admin/appsettings.json`:

```json
{
  "HonuaServer": {
    "BaseUrl": "https://your-server.com",
    "ApiKey": "your-api-key"
  }
}
```

## Features

### Form Designer
- Visual form builder with drag-and-drop interface
- OpenRosa-compatible XML export
- Integration with server layer schemas
- Mobile preview and testing

### Layer Management
- Schema visualization and editing
- Spatial reference system configuration
- Field validation rules
- Performance monitoring

### Service Administration
- Endpoint configuration
- Authentication management
- Rate limiting and quotas
- Health monitoring

## Contributing

This project follows the same contribution guidelines as [honua-server](https://github.com/honua-io/honua-server).

## License

Licensed under the Elastic License v2.0. See [LICENSE](LICENSE) for details.