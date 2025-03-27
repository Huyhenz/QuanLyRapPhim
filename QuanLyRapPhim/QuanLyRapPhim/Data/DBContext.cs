using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;    
using QuanLyRapPhim.Models;
namespace QuanLyRapPhim.Data
{
    public class DBContext : IdentityDbContext<User>
    {
        public DBContext(DbContextOptions<DBContext> options) : base(options)
        {
        }
        public DbSet<Models.Movie> Movies { get; set; }
        public DbSet<Models.Room> Rooms { get; set; }
        public DbSet<Models.Showtime> Showtimes { get; set; }
        public DbSet<Models.Seat> Seats { get; set; }
        public DbSet<Models.Booking> Bookings { get; set; }
        public DbSet<Models.BookingDetail> BookingDetails { get; set; }
        public DbSet<Models.Payment> Payments { get; set; }
    }
}
