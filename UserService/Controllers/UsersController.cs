using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Common;
using UserService.Data;
using UserService.Dtos;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserServiceContext _context;
    private readonly ReservationServiceClient _reservationServiceClient;

    public UsersController(UserServiceContext context, ReservationServiceClient reservationServiceClient)
    {
        _context = context;
        _reservationServiceClient = reservationServiceClient;
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.GetUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = "NOT_FOUND",
                Message = "User not found"
            });
        }

        var statistics = await _reservationServiceClient.GetStatisticsAsync(userId);

        return Ok(new ProfileResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role,
            MembershipStatus = user.MembershipStatus,
            MemberSince = user.MemberSince,
            ActiveReservations = statistics.ActiveReservations,
            BorrowingHistory = statistics.BorrowingHistory
        });
    }

    [HttpGet("{userId:guid}/validate")]
    public async Task<IActionResult> ValidateUser(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = "NOT_FOUND",
                Message = "User not found"
            });
        }

        if (user.MembershipStatus != MembershipStatus.Active)
        {
            return BadRequest(new ApiErrorResponse
            {
                Error = "USER_SUSPENDED",
                Message = "User account is suspended"
            });
        }

        var statistics = await _reservationServiceClient.GetStatisticsAsync(userId);

        return Ok(new UserValidationResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            MembershipStatus = user.MembershipStatus,
            ActiveReservationsCount = statistics.ActiveReservations
        });
    }
}
