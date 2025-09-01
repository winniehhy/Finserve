## 🏢 Overview

Finserve is a full-featured HR management system that provides role-based access control for different user types including Employees, HR staff, Senior HR, and Administrators. The system handles all aspects of employee lifecycle management from onboarding to payroll processing.

## ✨ Key Features

### 🔐 User Authentication & Authorization
- **Role-Based Access Control**: Employee, HR, Senior HR, Admin roles
- **Secure Login System**: ASP.NET Core Identity with email-based authentication
- **Password Reset**: Email-based password recovery system
- **Profile Management**: Users can update personal information and documents

### 👥 Employee Management
- **Employee Onboarding**: Complete employee registration with document upload
- **Employee Records**: Comprehensive employee database with personal, bank, and emergency contact information
- **Document Management**: Upload and manage employee documents (PDF, images, Word documents)
- **Nationality Support**: Special handling for Malaysian IC and international passport numbers
- **Employee ID Generation**: Automatic generation based on role (EM001, HR001, ADMIN001)

### 💰 Payroll Management
- **Payroll Processing**: Calculate and process employee salaries
- **Payslip Generation**: PDF payslip generation with detailed breakdown
- **Approval Workflow**: Multi-level approval system (HR → Senior HR)
- **Payroll History**: Complete payroll records and history tracking
- **Statutory Calculations**: EPF, income tax, and other deductions

### 📋 Claims Management
- **Claim Submission**: Employees can submit various types of claims
- **OCR Integration**: Tesseract OCR for receipt/document text extraction
- **Approval Workflow**: HR review and approval process
- **Claim Categories**: Support for different claim types (medical, travel, etc.)
- **Document Attachments**: Upload supporting documents for claims

### 🏖️ Leave Management
- **Leave Applications**: Employee leave request submission
- **Leave Types**: Support for multiple leave categories
- **Approval Process**: HR approval workflow
- **Leave Balance**: Track employee leave entitlements and usage
- **Leave History**: Complete leave records

### 📊 Reporting & Analytics
- **Dashboard Views**: Role-specific dashboards with key metrics
- **Payroll Reports**: Comprehensive payroll reporting
- **Employee Reports**: Employee data analytics and reporting
- **Invoice Management**: Admin-level invoice tracking and management

## 🛠️ Technology Stack

- **Framework**: ASP.NET Core 8.0 (Razor Pages/MVC)
- **Database**: MySQL with Entity Framework Core
- **Authentication**: ASP.NET Core Identity
- **Frontend**: Bootstrap 5, jQuery, Chart.js
- **PDF Generation**: Rotativa.AspNetCore
- **OCR**: Tesseract OCR
- **Email**: MailKit/SMTP
- **File Storage**: Local file system with organized folder structure

## 📋 Prerequisites

Before running this application, ensure you have:

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [MySQL Server](https://dev.mysql.com/downloads/mysql/) 8.0 or later
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/)
- [Git](https://git-scm.com/) for version control

## 🚀 Getting Started

### 1. Clone the Repository
```bash
git clone https://github.com/winniehhy/Finserve.git
cd Finserve/FinserveNew
```

### 2. Database Setup
1. Create a MySQL database named `finserve`
2. Update the connection string in `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=finserve;user=your_username;password=your_password;SslMode=None;Connect Timeout=60;Allow User Variables=True;Convert Zero Datetime=True;Allow Zero Datetime=True;"
  }
}
```

### 3. Configure Email Settings
Update the SMTP configuration in `appsettings.Development.json`:
```json
{
  "Smtp": {
    "Host": "your-smtp-host",
    "Port": 587,
    "User": "your-email@domain.com",
    "Pass": "your-password",
    "From": "noreply@yourcompany.com"
  }
}
```

### 4. Install Dependencies
```bash
dotnet restore
```

### 5. Apply Database Migrations
```bash
dotnet ef database update
```

### 6. Run the Application
```bash
dotnet run
```

## 👤 Default User Account

The system creates a default HR account for initial access:
- **Email**: hr@finserve.com
- **Password**: Test@123

⚠️ **Important**: This default account will be automatically deactivated once you create additional HR accounts.

## 📖 User Guide

### For Employees
1. **Login**: Use your employee ID or email and password
2. **Profile**: Update personal information, view documents
3. **Claims**: Submit expense claims with receipt uploads
4. **Leaves**: Apply for leave and track leave balance
5. **Payslips**: View and download monthly payslips

### For HR Staff
1. **Employee Management**: Add, edit, and manage employee records
2. **Payroll Processing**: Process monthly payrolls for employees
3. **Claims Review**: Review and approve/reject employee claims
4. **Leave Management**: Approve or reject leave applications
5. **Reports**: Generate employee and payroll reports

### For Senior HR
1. **Payroll Approval**: Final approval for processed payrolls
2. **Advanced Reports**: Access to comprehensive reporting
3. **Employee Oversight**: Full employee management capabilities

### For Administrators
1. **Invoice Management**: Manage company invoices
2. **System Reports**: Access to all system reports
3. **Advanced Analytics**: View system-wide metrics


## 🐛 Troubleshooting

### Common Issues

1. **Database Connection Error**
   - Verify MySQL server is running
   - Check connection string in `appsettings.json`
   - Ensure database exists and user has proper permissions

2. **Email Not Sending**
   - Verify SMTP settings in configuration
   - Check firewall settings for SMTP ports
   - Ensure email credentials are correct

### Development Tips

1. **Database Migrations**
   ```bash
   # Add new migration
   dotnet ef migrations add [MigrationName]
   
   # Apply migrations
   dotnet ef database update
   ```

2. **Logging**
   - Application logs are available in the console

3. **Debugging**
   - Use Visual Studio debugger
   - Check browser developer tools for client-side issues
