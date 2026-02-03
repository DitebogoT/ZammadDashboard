# Zammad SLA Dashboard

Real-time monitoring dashboard for Zammad helpdesk system, tracking SLA breaches, at-risk tickets, P1 priorities, and ticket trends.

## 🎯 Features

- **SLA Breach Monitoring** - Real-time alerts for tickets exceeding SLA
- **At-Risk Detection** - Early warning for tickets approaching SLA deadline
- **Priority Tracking** - Monitor all P1/Critical tickets
- **Aging Tickets** - Track tickets open > 48 hours
- **Trend Analysis** - Compare today's vs yesterday's ticket volume
- **Auto-Refresh** - Dashboard updates every 60 seconds
- **REST API** - JSON endpoints for integration

## 📊 Dashboard Metrics

| Metric | Description | Alert Level |
|--------|-------------|-------------|
| 🚨 SLA Breaches | Tickets that exceeded SLA | Critical |
| ⚠️ SLA At Risk | Within 60 min of breach | Warning |
| 🔥 Open P1 Tickets | All critical priority tickets | High |
| ⏰ Open > 48 Hours | Aging tickets needing attention | Medium |
| 📊 Daily Trend | Today vs Yesterday comparison | Info |

## 🚀 Quick Start

### Prerequisites
- .NET 6.0 or .NET 8.0 SDK
- Visual Studio 2022
- Zammad instance with API access

### Installation

1. **Clone/Create Project**
   ```bash
   # Create new ASP.NET Core MVC project
   dotnet new mvc -n ZammadDashboard
   cd ZammadDashboard
   ```

2. **Install Dependencies**
   ```bash
   dotnet add package Zammad.Client
   dotnet add package Microsoft.Extensions.Caching.Memory
   ```

3. **Configure Zammad Connection**
   
   Update `appsettings.json`:
   ```json
   {
     "Zammad": {
       "Url": "https://your-company.zammad.com",
       "Username": "api-user@company.com",
       "Password": "your-password",
       "P1PriorityId": 1,
       "ClosedStateId": 4
     }
   }
   ```

4. **Run Application**
   ```bash
   dotnet run
   ```
   
   Open browser: `https://localhost:5001`

## 📁 Project Structure

```
ZammadDashboard/
├── Controllers/
│   ├── HomeController.cs              # Main dashboard
│   └── DashboardApiController.cs      # REST API
├── Models/
│   └── DashboardModels.cs             # Data models
├── Services/
│   ├── ZammadDashboardService.cs      # Zammad integration
│   └── MockDashboardService.cs        # Testing mock
├── Views/
│   └── Home/
│       └── Index.cshtml               # Dashboard UI
├── appsettings.json                   # Configuration
└── Program.cs                         # App startup
```

## 🔧 Configuration

### Required Settings

| Setting | Description | Example |
|---------|-------------|---------|
| `Url` | Zammad instance URL | `https://company.zammad.com` |
| `Username` | API user email | `dashboard@company.com` |
| `Password` | API user password | `SecurePassword123` |
| `P1PriorityId` | Critical priority ID | `1` |
| `ClosedStateId` | Closed state ID | `4` |

### Optional Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `RefreshIntervalSeconds` | Dashboard refresh rate | `60` |
| `SlaWarningThresholdMinutes` | At-risk threshold | `60` |
| `CacheExpirationSeconds` | API cache duration | `30` |

### Finding Your Zammad IDs

**Priority IDs:**
1. Login to Zammad as admin
2. Navigate to: Admin → Objects → Ticket → Priority
3. Note the ID for your P1/Critical priority

**State IDs:**
1. Navigate to: Admin → Objects → Ticket → State
2. Note the ID for "Closed" state

## 🧪 Testing

### Using Mock Service (No Zammad Required)

For testing the UI without Zammad connection:

1. In `Program.cs`, replace:
   ```csharp
   builder.Services.AddSingleton<IZammadDashboardService, ZammadDashboardService>();
   ```
   
   With:
   ```csharp
   builder.Services.AddSingleton<IZammadDashboardService, MockDashboardService>();
   ```

2. Run the application - you'll see simulated data

3. **Remember to switch back before production!**

## 🌐 API Endpoints

### Get Dashboard Metrics
```http
GET /api/DashboardApi/metrics
```

**Response:**
```json
{
  "slaBreaches": 3,
  "slaAtRisk": 5,
  "openP1Tickets": 2,
  "ticketsOpenMoreThan48Hours": 12,
  "todayTicketCount": 45,
  "yesterdayTicketCount": 38,
  "lastUpdated": "2024-01-09T14:30:00",
  "slaBreachTickets": [...],
  "slaAtRiskTickets": [...],
  "p1Tickets": [...],
  "tickets48HoursPlus": [...]
}
```

### Health Check
```http
GET /api/DashboardApi/health
```

### Force Refresh
```http
POST /api/DashboardApi/refresh
```

## 🔐 Security Best Practices

### Production Deployment

1. **Never commit credentials**
   ```bash
   # Add to .gitignore
   appsettings.json
   appsettings.*.json
   ```

2. **Use Environment Variables**
   ```bash
   export Zammad__Username="api-user@company.com"
   export Zammad__Password="SecurePassword123"
   ```

3. **Use Azure Key Vault** (Recommended)
   ```csharp
   builder.Configuration.AddAzureKeyVault(
       new Uri("https://your-vault.vault.azure.net/"),
       new DefaultAzureCredential()
   );
   ```

4. **Add Authentication**
   ```csharp
   builder.Services.AddAuthentication()
       .AddCookie();
   ```

## 📈 Performance

### Caching Strategy
- **API Cache:** 30 seconds (configurable)
- **Browser Cache:** Disabled for real-time updates
- **Ticket List Limit:** Top 10 per category

### Optimization Tips
1. Increase `CacheExpirationSeconds` for less frequent updates
2. Reduce ticket list limits if performance issues
3. Use database for historical data (not implemented)

## 🐛 Troubleshooting

### Common Issues

**"Cannot connect to Zammad"**
- Verify URL includes `https://`
- Check username/password are correct
- Ensure API user has proper permissions

**"No tickets showing"**
- Verify `P1PriorityId` matches your Zammad setup
- Check `ClosedStateId` is correct
- Confirm there are actually open tickets

**"Metrics showing zeros"**
- Check application logs for errors
- Verify Zammad SLA policies are configured
- Test API endpoint directly: `/api/DashboardApi/metrics`

### Debugging

Enable detailed logging in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "ZammadDashboard": "Debug"
    }
  }
}
```

## 🚀 Deployment

### IIS Deployment

1. **Publish**
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Configure IIS**
   - Create new site
   - Point to publish folder
   - Set application pool to .NET Core
   - Configure HTTPS binding

### Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY publish/ .
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "ZammadDashboard.dll"]
```

```bash
docker build -t zammad-dashboard .
docker run -p 8080:80 zammad-dashboard
```

### Azure App Service

```bash
az webapp create --resource-group myResourceGroup \
  --plan myAppServicePlan \
  --name zammad-dashboard \
  --runtime "DOTNET|8.0"

dotnet publish -c Release
cd bin/Release/net8.0/publish
zip -r deploy.zip .
az webapp deploy --resource-group myResourceGroup \
  --name zammad-dashboard \
  --src-path deploy.zip
```

## 🔄 Future Enhancements

### Planned Features
- [ ] Email alerts for SLA breaches
- [ ] SMS notifications
- [ ] Historical data tracking (database)
- [ ] Custom date range filters
- [ ] Export to Excel/PDF
- [ ] SignalR for real-time updates
- [ ] Mobile app (React Native)
- [ ] Multi-tenant support
- [ ] Custom SLA rules
- [ ] Team/Agent filters

### Contribution Ideas
- Add unit tests
- Implement authentication
- Create admin panel
- Add dark mode
- Improve mobile responsiveness

## 📝 License

This project is licensed under the MIT License.

## 👥 Support

For issues and questions:
- Check troubleshooting section
- Review Zammad API documentation: https://docs.zammad.org/
- Open an issue on GitHub

## 🙏 Acknowledgments

- Built with ASP.NET Core MVC
- Powered by Zammad.Client library
- Icons: Unicode emoji (no dependencies)

---

**Made with ❤️ for better SLA management**
