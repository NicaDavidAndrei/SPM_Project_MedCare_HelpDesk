using MedCare_HelpDesk.Models;
using MedCare_HelpDesk.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MedCare_HelpDesk.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;

        public UsersController(IUserService userService, IDepartmentService departmentService)
        {
            _userService = userService;
            _departmentService = departmentService;
        }

        public async Task<IActionResult> Index(string? searchString)
        {
            ViewData["CurrentFilter"] = searchString;
            var users = await _userService.GetFilteredUsersAsync(searchString);
            return View(users);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var user = await _userService.GetUserByIdWithDepartmentAsync(id.Value);
            return user == null ? NotFound() : View(user);
        }

        public async Task<IActionResult> Create()
        {
            ViewData["DepartmentId"] = new SelectList(_departmentService.GetAllDepartments(activeOnly: true), "Id", "Name");
            ViewData["Roles"] = new SelectList(await _userService.GetAllRolesAsync());
            return View(new User { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FirstName,LastName,Email,PhoneNumber,DepartmentId,Role,IsActive")] User formUser, string? PasswordHash)
        {
            ModelState.Clear();
            TryValidateModel(formUser);

            if (ModelState.IsValid)
            {
                string finalPassword = string.IsNullOrEmpty(PasswordHash) ? "MedCare123!" : PasswordHash;
                var result = await _userService.CreateUserWithRoleAsync(formUser, finalPassword, formUser.Role ?? "User");
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Hospital staff account created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewData["DepartmentId"] = new SelectList(_departmentService.GetAllDepartments(activeOnly: true), "Id", "Name", formUser.DepartmentId);
            ViewData["Roles"] = new SelectList(await _userService.GetAllRolesAsync(), formUser.Role);
            return View(formUser);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var user = await _userService.GetUserByIdWithDepartmentAsync(id.Value);
            if (user == null) return NotFound();
            await PrepareEditViewBag(user);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FirstName,LastName,Email,PhoneNumber,DepartmentId,Role,IsActive")] User formUser, string? PasswordHash)
        {
            if (id != formUser.Id) return NotFound();
            ModelState.Clear();
            TryValidateModel(formUser);

            if (ModelState.IsValid)
            {
                var result = await _userService.UpdateUserWithRoleAsync(formUser, formUser.Role ?? "User", PasswordHash);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Staff account updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            }

            await PrepareEditViewBag(formUser);
            return View(formUser);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var result = await _userService.ToggleUserActiveAsync(id);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                result.Succeeded ? "User activation status updated." : "Could not toggle user status.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var user = await _userService.GetUserByIdWithDepartmentAsync(id.Value);
            return user == null ? NotFound() : View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var result = await _userService.DeleteUserAsync(id);
                TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
                    ? "User and related personal data deleted."
                    : "Could not delete user.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Constraint error: " + (ex.InnerException?.Message ?? ex.Message);
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task PrepareEditViewBag(User user)
        {
            ViewData["DepartmentId"] = new SelectList(_departmentService.GetAllDepartments(), "Id", "Name", user.DepartmentId);
            ViewData["Roles"] = new SelectList(await _userService.GetAllRolesAsync(), user.Role);
        }
    }
}
