using System;
using System.Collections.Generic;

namespace FinserveNew;

public partial class Employee
{
    public string EmployeeId { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Ic { get; set; } = null!;

    public string Nationality { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string TelephoneNumber { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public DateOnly? JoinDate { get; set; }

    public DateOnly? ResignationDate { get; set; }

    public string ConfirmationStatus { get; set; } = null!;

    public string IncomeTaxNumber { get; set; } = null!;

    public string Epfnumber { get; set; } = null!;
}
