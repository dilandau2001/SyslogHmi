# SyslogHmi - Syslog Viewer & Management Application

A modern, feature-rich desktop application for monitoring, filtering, and analyzing syslog messages in real-time. Built with WPF and .NET 10, SyslogHmi provides a comprehensive graphical interface for receiving syslog messages over TCP and UDP protocols, storing them in a SQLite database, and managing visual formatting rules.

<img width="1370" height="758" alt="image" src="https://github.com/user-attachments/assets/5918762b-0ac5-4ffc-a44a-00c28658e848" />

## Table of Contents

- [Features](#features)
- [System Requirements](#system-requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Usage](#usage)
  - [Network Configuration](#network-configuration)
  - [Message Filtering](#message-filtering)
  - [Color Rules & Formatting](#color-rules--formatting)
  - [SQL Analysis](#sql-analysis)
- [Architecture](#architecture)
- [Technologies](#technologies)
- [Project Structure](#project-structure)
- [License](#license)

## Features

### Core Functionality
- **Dual-Protocol Support**: Receive syslog messages over both TCP and UDP simultaneously
- **Real-Time Monitoring**: Display incoming messages with low-latency updates
- **Message Persistence**: Store all received messages in a SQLite database for historical analysis
- **High-Volume Handling**: Optimized batch processing and UI update mechanisms for thousands of messages
- **Professional UI**: Clean, intuitive WPF-based interface with toolbar controls and tabbed views

### Filtering & Search
- **Multi-Criteria Filtering**: Filter messages by:
  - Hostname
  - Application name
  - Message content
  - Severity level (0-7)
  - Time range with customizable hour/minute selectors
- **Dynamic View Updates**: Filtered results update automatically as filter criteria change
- **Live Message Count**: Track total received messages and filtered message counts

### Color Rules & Formatting
- **Visual Highlighting**: Define custom color rules to highlight messages based on patterns
- **Priority-Based Rules**: Rules are applied in order of priority; first matching rule wins
- **Rule Management**: Add, edit, delete, and reorder rules through an intuitive interface
- **Condition Types**: Support for various matching conditions:
  - Text pattern matching
  - Facility code matching
  - Severity level matching
  - Custom color assignment

### Advanced Features
- **SQL Query Analysis**: Execute custom SQL queries directly against the message database
- **Message Test Generator**: Send random test messages for application validation
- **Message Statistics**: View comprehensive statistics and analysis of message patterns
- **Database Persistence**: Full message history retained in local SQLite database

## System Requirements

- **Operating System**: Windows 7 or later (with .NET 10 runtime)
- **.NET Framework**: .NET 10.0-windows or later
- **RAM**: Minimum 512 MB (1 GB recommended for high-volume environments)
- **Storage**: 500 MB for installation; additional space for message database (varies by volume)
- **Network**: TCP/UDP port availability for syslog reception (default: 514 for both)

## Installation

1. **Clone the Repository**
   ```powershell
   git clone https://github.com/dilandau2001/SyslogHmi.git
   cd SyslogApp
   ```

2. **Open in Visual Studio**
   - Open `SyslogHmi.sln` in Visual Studio 2022 or later

3. **Build the Solution**
   ```powershell
   dotnet build
   ```

4. **Run the Application**
   ```powershell
   dotnet run --project SyslogHmi/SyslogHmi.csproj
   ```

Alternatively, use the compiled executable from the Release folder.

## Quick Start

1. **Start the Application**
   - Launch `SyslogHmi.exe`

2. **Configure Network Ports**
   - Set TCP Port (default: 514)
   - Set UDP Port (default: 514)
   - Click **START** to begin listening

3. **Send Syslog Messages**
   - Configure your devices/applications to send syslog messages to your machine on the configured ports
   - Or use the test message generator (see Usage section)

4. **Monitor Messages**
   - Messages appear in real-time in the main Messages view
   - Use filters to narrow down results
   - Apply color rules for visual organization

## Usage

### Network Configuration

The toolbar at the top of the application provides network settings:

- **TCP Port**: Port number for TCP connections (default: 514)
- **UDP Port**: Port number for UDP datagrams (default: 514)
- **START Button**: Begin listening for incoming syslog messages
- **STOP Button**: Stop the listener and disconnect

Both TCP and UDP listeners operate concurrently, allowing messages from both protocols to be received simultaneously.

### Message Filtering

The Messages tab includes a comprehensive filtering panel:

1. **Hostname Filter**: Enter hostname or partial match
2. **Application Name Filter**: Search by application/process name
3. **Message Content Filter**: Search message text
4. **Severity Level**: Select specific severity level (Emergency to Debug)
5. **Time Range**: 
   - Set start and end times with hour/minute selectors
   - Useful for analyzing message patterns within specific timeframes
6. **Facility Selection**: Filter by facility codes (kernel messages, user-level messages, etc.)

Filters are applied in real-time; matching messages are displayed in the filtered view below.

### Color Rules & Formatting

The Color Rules tab allows creation of visual formatting rules:

1. **Create New Rule**
   - Click "New Rule" to reset the form
   - Enter rule name and description

2. **Define Conditions**
   - Add multiple conditions to the rule
   - Condition types include:
     - Message text matching (substring or regex)
     - Severity level matching
     - Facility code matching

3. **Set Visual Properties**
   - Assign foreground color
   - Assign background color
   - Optionally bold the text

4. **Manage Priority**
   - Use Move Up/Move Down buttons to adjust rule order
   - Rules are evaluated top-to-bottom; first match applies

5. **Persistence**
   - Rules are automatically saved to the database
   - Rules persist across application restarts

### SQL Analysis

The SQL Analysis tab provides direct database access:

1. **Execute Custom Queries**
   - Write SQL queries to analyze syslog data
   - Query the `SyslogMessages` table directly

2. **Available Columns** in the database:
   - `Id`: Message identifier
   - `Timestamp`: Original message timestamp
   - `Hostname`: Source hostname
   - `AppName`: Application/process name
   - `ProcessId`: Process identifier
   - `Severity`: Numeric severity (0-7)
   - `Facility`: Numeric facility (0-23)
   - `SeverityName`: Human-friendly severity
   - `FacilityName`: Human-friendly facility
   - `Message`: Short message text
   - `FullMessage`: Complete original message
   - `ReceivedTime`: Time received by application

3. **Example Queries**
   ```sql
   -- Count messages by severity
   SELECT SeverityName, COUNT(*) FROM SyslogMessages GROUP BY SeverityName

   -- Find errors from specific host
   SELECT * FROM SyslogMessages WHERE Hostname = 'server01' AND Severity <= 3

   -- Messages in last hour
   SELECT * FROM SyslogMessages WHERE ReceivedTime > datetime('now', '-1 hour')
   ```

## Architecture

SyslogHmi follows the **MVVM (Model-View-ViewModel)** design pattern with clear separation of concerns:

### Core Components

**Services Layer**
- `SyslogListener`: Dual-protocol network listener (TCP/UDP) for message reception
- `DatabaseService`: SQLite-based persistence layer for messages and rules
- `SyslogQueueManager`: Batch queue management for efficient message processing
- `SyslogMessageSender`: Test message generator for validation

**View Model Layer**
- `MainViewModel`: Orchestrates overall application flow and message management
- `FilterViewModel`: Manages filter criteria and applies filtering logic
- `ColorRuleViewModel`: Manages color rules collection and operations
- `ColorRuleFormViewModel`: Handles rule creation/editing form state
- `SqlAnalysisViewModel`: Manages SQL query execution and results

**Models**
- `SyslogMessage`: Represents a parsed syslog message with all metadata
- `ColorRule`: Defines visual formatting rules and conditions
- `ColorCondition`: Individual condition within a color rule
- `ColorFormat`: Visual properties (colors, font weights)

**Views (WPF)**
- `MainWindow`: Primary application window and layout
- `MessagesView`: Message display and filtering interface
- `ColorRulesView`: Color rule management interface
- `SqlAnalysisView`: Database query interface

### Design Patterns

- **MVVM Pattern**: Clear separation between UI and business logic
- **Dependency Injection**: Services are injected into view models
- **Event-Driven Architecture**: Events propagate changes from network layer to UI
- **Batch Processing**: Messages are batched for both UI updates and database writes
- **INotifyPropertyChanged**: Reactive property binding for UI updates
- **ICommand Pattern**: User actions are routed through command objects

## Technologies

### Core Framework
- **.NET 10** (Windows Desktop)
- **WPF (Windows Presentation Foundation)** for UI
- **C# 13** language features

### Data & Persistence
- **Microsoft.Data.Sqlite** (v10.0.8): SQLite database access
- **Microsoft.EntityFrameworkCore** (v10.0.8): ORM capabilities

### Utilities & Logging
- **Serilog** (v4.3.1): Structured logging framework
- **Community Toolkit.MVVM** (v8.4.2): MVVM helper utilities

### Architecture
- **Async/Await**: Comprehensive asynchronous processing
- **Task Parallel Library**: Multi-threaded message processing
- **ConcurrentCollections**: Thread-safe message queues

## Project Structure

```
SyslogHmi/
├── Models/
│   ├── SyslogMessage.cs              # Core syslog message model
│   ├── ColorRule.cs                  # Color rule definition
│   ├── ColorCondition.cs             # Rule condition
│   ├── ColorFormat.cs                # Visual formatting properties
│   └── ColorItem.cs
├── Services/
│   ├── SyslogListener.cs             # TCP/UDP network listener
│   ├── DatabaseService.cs            # SQLite persistence
│   ├── SyslogQueueManager.cs         # Message batch processing
│   ├── SyslogMessageSender.cs        # Test message generator
│   └── IDatabaseService.cs           # Interface definition
├── ViewModels/
│   ├── MainViewModel.cs              # Main orchestrator
│   ├── FilterViewModel.cs            # Filtering logic
│   ├── ColorRuleViewModel.cs         # Rule management
│   ├── ColorRuleFormViewModel.cs     # Form state management
│   ├── SqlAnalysisViewModel.cs       # Database query execution
│   ├── ViewModelBase.cs              # MVVM base class
│   ├── RelayCommand.cs               # Command implementation
│   ├── BulkObservableCollection.cs   # Optimized collection
│   ├── FacilityCheckItem.cs          # Facility filter item
│   └── SeverityOptionViewModel.cs    # Severity option item
├── Views/
│   ├── MainWindow.xaml(.cs)          # Main application window
│   ├── MessagesView.xaml(.cs)        # Message display view
│   ├── ColorRulesView.xaml(.cs)      # Color rules interface
│   └── SqlAnalysisView.xaml(.cs)     # SQL analysis interface
├── Converters/
│   ├── UtcToLocalConverter.cs        # DateTime conversion
│   ├── FriendlyColorNameToBrushConverter.cs
│   ├── StringToColorBrushConverter.cs
│   ├── NullToVisibilityConverter.cs  # Null-to-visibility binding
│   └── SingleLineConverter.cs
├── Helpers/
│   └── ColorHelper.cs                # Color utilities
├── Properties/
│   └── Resources.resx                # Application resources
├── App.xaml(.cs)                     # Application startup
└── SyslogHmi.csproj                  # Project configuration
```

## Performance Considerations

### High-Volume Message Handling

The application is optimized for high-volume syslog reception:

1. **Dual-Queue System**: Separate queues for database writes and UI updates prevent blocking
2. **Batch Processing**: Messages are batched before database insertion (default: 100 messages)
3. **UI Update Throttling**: UI refreshes occur at fixed intervals to prevent blocking
4. **Concurrent Processing**: Network listening, database writes, and UI updates happen on separate threads
5. **Maximum Message Cap**: UI display is capped at 99,999 messages to prevent memory issues (older messages are removed)

### Memory Management

- **IDisposable Pattern**: Services properly dispose network resources and database connections
- **Async Disposal**: Supports both synchronous and asynchronous disposal
- **Circular Buffer**: Message collection uses aging strategy to maintain consistent memory usage

## Development & Contribution

### Building from Source

```powershell
# Clone and navigate
git clone https://github.com/dilandau2001/SyslogHmi.git
cd SyslogApp

# Restore and build
dotnet restore
dotnet build

# Run in debug mode
dotnet run --project SyslogHmi/SyslogHmi.csproj
```

### Database Schema

The SQLite database is automatically initialized on first run. The main table structure includes:

- **SyslogMessages**: Stores all received messages with full metadata
- **ColorRules**: Persists color rule definitions and priorities
- **ColorConditions**: Stores individual rule conditions
- **ColorFormats**: Stores visual format definitions

### Code Style

The project follows standard C# conventions:
- PascalCase for public members
- camelCase for private members
- XML documentation comments for public APIs
- MVVM pattern adherence throughout

## Troubleshooting

### Port Already in Use
If you receive a "port already in use" error:
1. Check if another instance of SyslogHmi is running
2. Use Resource Monitor to identify the process using the port
3. Configure a different port in the UI (e.g., 1514 instead of 514)

### No Messages Received
1. Verify firewall isn't blocking TCP/UDP on configured ports
2. Confirm syslog source is configured with correct destination IP and port
3. Check that START button has been clicked to activate the listener

### Database Issues
- Database file location: `%APPDATA%\SyslogHmi\syslog_messages.db`
- If corrupted, delete the `.db` file to reset (starts fresh on next run)

### Performance Issues with Large Datasets
1. Use filtering to view smaller subsets
2. Execute SQL analysis queries to aggregate data instead of loading full message list
3. Periodically archive or delete old messages via SQL analysis tab

## License

This project is provided as-is for use. Please refer to the repository for specific license terms.

## Contact & Support

For issues, feature requests, or contributions, please visit:
[GitHub Repository](https://github.com/dilandau2001/SyslogHmi)

---

**Version**: 1.0  
**Platform**: Windows (.NET 10)  
**Last Updated**: 2024
