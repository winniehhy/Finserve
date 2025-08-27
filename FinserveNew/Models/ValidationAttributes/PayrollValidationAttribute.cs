using System.ComponentModel.DataAnnotations;
using FinserveNew.Models.ViewModels;

namespace FinserveNew.Models.ValidationAttributes
{
    /// <summary>
    /// Custom validation attribute for payroll data validation
    /// </summary>
    public class PayrollValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value is PayrollProcessViewModel payroll)
            {
                return ValidatePayrollData(payroll);
            }
            return false;
        }

        private bool ValidatePayrollData(PayrollProcessViewModel payroll)
        {
            // Check that total deductions don't exceed basic salary
            var totalDeductions = payroll.EmployeeEpf + payroll.EmployeeSocso + 
                                 payroll.EmployeeEis + payroll.EmployeeTax;
            
            if (totalDeductions >= payroll.BasicSalary)
            {
                ErrorMessage = "Total employee deductions cannot equal or exceed basic salary.";
                return false;
            }

            // Validate EPF percentages
            if (payroll.BasicSalary > 0)
            {
                var employerEpfPercentage = (payroll.EmployerEpf / payroll.BasicSalary) * 100;
                var employeeEpfPercentage = (payroll.EmployeeEpf / payroll.BasicSalary) * 100;

                if (employerEpfPercentage > 20)
                {
                    ErrorMessage = "Employer EPF contribution exceeds 20% of basic salary.";
                    return false;
                }

                if (employeeEpfPercentage > 15)
                {
                    ErrorMessage = "Employee EPF contribution exceeds 15% of basic salary.";
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Custom validation attribute for employee age validation
    /// </summary>
    public class AgeRangeAttribute : ValidationAttribute
    {
        private readonly int _minimumAge;
        private readonly int _maximumAge;

        public AgeRangeAttribute(int minimumAge, int maximumAge)
        {
            _minimumAge = minimumAge;
            _maximumAge = maximumAge;
        }

        public override bool IsValid(object? value)
        {
            if (value is DateOnly dateOfBirth)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var age = today.Year - dateOfBirth.Year;
                
                if (dateOfBirth > today.AddYears(-age))
                    age--;
                
                return age >= _minimumAge && age <= _maximumAge;
            }
            return false;
        }

        public override string FormatErrorMessage(string name)
        {
            return $"Age must be between {_minimumAge} and {_maximumAge} years.";
        }
    }

    /// <summary>
    /// Custom validation attribute for Malaysian IC format
    /// </summary>
    public class MalaysianICAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null || string.IsNullOrEmpty(value.ToString()))
                return true; // Let Required attribute handle null/empty

            var ic = value.ToString()!;
            
            // Remove any hyphens for validation
            ic = ic.Replace("-", "");
            
            // Must be exactly 12 digits
            if (ic.Length != 12 || !ic.All(char.IsDigit))
                return false;

            // Extract date components
            var year = int.Parse(ic.Substring(0, 2));
            var month = int.Parse(ic.Substring(2, 2));
            var day = int.Parse(ic.Substring(4, 2));

            // Determine full year (assuming ICs are for people born after 1900)
            var fullYear = year < 30 ? 2000 + year : 1900 + year;

            // Validate date
            try
            {
                var birthDate = new DateTime(fullYear, month, day);
                var today = DateTime.Today;
                
                // Must be a valid date and person must be between 0-120 years old
                return birthDate <= today && birthDate >= today.AddYears(-120);
            }
            catch
            {
                return false;
            }
        }

        public override string FormatErrorMessage(string name)
        {
            return "Please enter a valid Malaysian IC number (format: XXXXXX-XX-XXXX).";
        }
    }

    /// <summary>
    /// Custom validation attribute for future date validation
    /// </summary>
    public class FutureDateValidationAttribute : ValidationAttribute
    {
        private readonly int _maxDaysInFuture;

        public FutureDateValidationAttribute(int maxDaysInFuture = 0)
        {
            _maxDaysInFuture = maxDaysInFuture;
        }

        public override bool IsValid(object? value)
        {
            if (value is DateOnly date)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var maxFutureDate = today.AddDays(_maxDaysInFuture);
                
                return date <= maxFutureDate;
            }
            return true;
        }

        public override string FormatErrorMessage(string name)
        {
            if (_maxDaysInFuture == 0)
                return $"{name} cannot be in the future.";
            else
                return $"{name} cannot be more than {_maxDaysInFuture} days in the future.";
        }
    }

    /// <summary>
    /// Custom validation attribute for conditional required fields
    /// </summary>
    public class ConditionalRequiredAttribute : ValidationAttribute
    {
        private readonly string _conditionalProperty;
        private readonly object _conditionalValue;

        public ConditionalRequiredAttribute(string conditionalProperty, object conditionalValue)
        {
            _conditionalProperty = conditionalProperty;
            _conditionalValue = conditionalValue;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var property = validationContext.ObjectType.GetProperty(_conditionalProperty);
            if (property == null)
                return new ValidationResult($"Property {_conditionalProperty} not found.");

            var conditionalValue = property.GetValue(validationContext.ObjectInstance);
            
            if (conditionalValue?.ToString() == _conditionalValue?.ToString())
            {
                if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                {
                    return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} is required.");
                }
            }

            return ValidationResult.Success;
        }
    }
}