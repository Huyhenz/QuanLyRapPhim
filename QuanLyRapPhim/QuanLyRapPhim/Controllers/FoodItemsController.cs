using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;

namespace QuanLyRapPhim.Controllers
{
    [Authorize(Roles = "Admin")]
    public class FoodItemsController : Controller
    {
        private readonly DBContext _context;

        public FoodItemsController(DBContext context)
        {
            _context = context;
        }

        // GET: FoodItems
        public async Task<IActionResult> Index()
        {
            return View(await _context.FoodItems.ToListAsync());
        }

        // GET: FoodItems/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: FoodItems/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FoodItemId,Name,Price,Size,ImageUrl,Category")] FoodItem foodItem)
        {
            if (ModelState.IsValid)
            {
                _context.Add(foodItem);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm món ăn thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(foodItem);
        }

        // GET: FoodItems/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var foodItem = await _context.FoodItems.FindAsync(id);
            if (foodItem == null) return NotFound();

            return View(foodItem);
        }

        // POST: FoodItems/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("FoodItemId,Name,Price,Size,ImageUrl,Category")] FoodItem foodItem)
        {
            if (id != foodItem.FoodItemId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(foodItem);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật món ăn thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FoodItemExists(foodItem.FoodItemId))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(foodItem);
        }

        // GET: FoodItems/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var foodItem = await _context.FoodItems.FirstOrDefaultAsync(m => m.FoodItemId == id);
            if (foodItem == null) return NotFound();

            return View(foodItem);
        }

        // POST: FoodItems/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var foodItem = await _context.FoodItems.FindAsync(id);
            if (foodItem != null)
            {
                _context.FoodItems.Remove(foodItem);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa món ăn thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool FoodItemExists(int id)
        {
            return _context.FoodItems.Any(e => e.FoodItemId == id);
        }
    }
}