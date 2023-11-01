using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;

namespace OnlineMusicLibrary;

[Index(nameof(token))]
public class User {
    [Key]
    public required string username { get; init; }
    public required string token { get; set; }
}
