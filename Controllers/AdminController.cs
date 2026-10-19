using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetMvcAuthV2.Constants;

namespace NetMvcAuthV2.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController : Controller
{
    public IActionResult Index() => View();
}
