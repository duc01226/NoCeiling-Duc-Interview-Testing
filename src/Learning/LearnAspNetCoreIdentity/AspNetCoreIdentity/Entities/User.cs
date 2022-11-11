using Microsoft.AspNetCore.Identity;

namespace AspNetCoreIdentity.Entities;

// Demo extend the IdentityUser to store more information if needed
public class User : IdentityUser
{
    public string Department { get; set; } = "";
    public string Position { get; set; } = "";

    public User WithDepartment(string department)
    {
        Department = department;
        return this;
    }

    public User WithPosition(string position)
    {
        Position = position;
        return this;
    }
}
