using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models.ViewModels;

namespace FinserveNew.Security
{
    /// <summary>
    /// Custom authorization filter to prevent role tampering in profile updates
    /// </summary>
    public class PreventRoleTamperingAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var actionArguments = context.ActionArguments;
            
            // Check if this is a POST request with EmployeeDetailsViewModel
            if (context.HttpContext.Request.Method == "POST" && 
                actionArguments.ContainsKey("vm") && 
                actionArguments["vm"] is EmployeeDetailsViewModel viewModel)
            {
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                
                // Get the current employee data from database
                var existingEmployee = dbContext.Employees
                    .Include(e => e.Role)
                    .FirstOrDefault(e => e.EmployeeID == viewModel.EmployeeID);
                
                if (existingEmployee != null)
                {
                    // Check if any unauthorized fields are being tampered with
                    var tamperedFields = new List<string>();
                    
                    // Check critical employment fields that should never change from profile
                    if (viewModel.Position != existingEmployee.Position)
                        tamperedFields.Add("Position");
                    
                    if (viewModel.ConfirmationStatus != existingEmployee.ConfirmationStatus)
                        tamperedFields.Add("ConfirmationStatus");
                    
                    if (viewModel.JoinDate != existingEmployee.JoinDate)
                        tamperedFields.Add("JoinDate");
                    
                    if (viewModel.Email != existingEmployee.Email)
                        tamperedFields.Add("Email");
                    
                    // If any tampering detected, log and reject the request
                    if (tamperedFields.Any())
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<PreventRoleTamperingAttribute>>();
                        logger.LogWarning("Potential security violation: User {EmployeeID} attempted to modify restricted fields: {Fields} at {Time}",
                            viewModel.EmployeeID, string.Join(", ", tamperedFields), DateTime.Now);
                        
                        context.Result = new BadRequestObjectResult(new 
                        { 
                            error = "Unauthorized attempt to modify restricted employment information.",
                            code = "SECURITY_VIOLATION"
                        });
                        return;
                    }
                }
            }
            
            base.OnActionExecuting(context);
        }
    }

    /// <summary>
    /// Custom authorization attribute specifically for profile-related actions
    /// </summary>
    public class ProfileSecurityAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Add security headers
            context.HttpContext.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.HttpContext.Response.Headers.Add("X-Frame-Options", "DENY");
            context.HttpContext.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
            
            base.OnActionExecuting(context);
        }
    }
}