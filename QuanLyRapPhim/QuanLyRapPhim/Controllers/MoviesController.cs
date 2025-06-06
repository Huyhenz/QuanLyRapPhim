﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;

namespace QuanLyRapPhim.Controllers
{
    
    [Authorize]
    public class MoviesController : Controller
    {
        private readonly DBContext _context;
        private readonly UserManager<User> _userManager;

        public MoviesController(DBContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        [Authorize(Roles = "Admin,User")]
        // GET: Movies
        public async Task<IActionResult> Index(int page = 1, string date = null)
        {
            int pageSize = 5;
            var moviesQuery = _context.Movies.Include(m => m.Reviews).AsQueryable();

            // Filter by date if needed (e.g., movies with showtimes on the selected date)
            if (!string.IsNullOrEmpty(date))
            {
                // Add filtering logic here (e.g., join with a Showtimes table)
            }

            int totalMovies = await moviesQuery.CountAsync();
            var movies = await moviesQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalMovies = totalMovies;

            return View(movies);
        }
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int movieId, string comment, int rating)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("Details", new { id = movieId });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

   
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.MovieId == movieId && r.UserId == user.Id);

            if (existingReview != null)
            {
                
                TempData["ErrorMessage"] = "Bạn đã đánh giá phim này rồi. Mỗi người dùng chỉ được đánh giá một lần.";
                return RedirectToAction("Details", new { id = movieId });
            }

         
            var review = new Review
            {
                MovieId = movieId,
                UserId = user.Id,
                Comment = comment,
                Rating = rating,
                CreatedAt = DateTime.Now
            };
            _context.Reviews.Add(review);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đánh giá của bạn đã được gửi thành công!";
            return RedirectToAction("Details", new { id = movieId });
        }
        
        
        // GET: Movies/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movies
                .Include(m => m.Reviews)
                .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null)
            {
                return NotFound();
            }

            return View(movie);
        }
        [Authorize(Roles = "Admin")]
        // GET: Movies/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Movies/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MovieId,Title,Description,Duration,Poster,Genre,Director,Actors,TrailerUrl")] Movie movie)
        {
            if (ModelState.IsValid)
            {
                _context.Add(movie);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(movie);
        }
        [Authorize(Roles = "Admin")]
        // GET: Movies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movies.FindAsync(id);
            if (movie == null)
            {
                return NotFound();
            }
            return View(movie);
        }

        // POST: Movies/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MovieId,Title,Description,Duration,Poster,Genre,Director,Actors,TrailerUrl")] Movie movie)
        {
            if (id != movie.MovieId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(movie);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MovieExists(movie.MovieId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(movie);
        }

        // GET: Movies/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movies
                .FirstOrDefaultAsync(m => m.MovieId == id);
            if (movie == null)
            {
                return NotFound();
            }

            return View(movie);
        }

        // POST: Movies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie != null)
            {
                _context.Movies.Remove(movie);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MovieExists(int id)
        {
            return _context.Movies.Any(e => e.MovieId == id);
        }


    }
}
