using AuthAPIwithController.Dtos;
using AuthAPIwithController.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthAPIwithController.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DBUserController : ControllerBase
    {
        private readonly UserManager<User> _userManager;

        public DBUserController(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet("List")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetAllUser()
        {
            var users = await _userManager.Users.ToListAsync();

            var userList = new List<UserWithRolesDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                userList.Add(new UserWithRolesDto
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Roles = roles
                });
            }

            return Ok(userList);
        }

        [HttpGet("MyProfile")]
        public async Task<IActionResult> GetUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound("User not found");

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                Roles = await _userManager.GetRolesAsync(user)
            });
        }
    }

}
