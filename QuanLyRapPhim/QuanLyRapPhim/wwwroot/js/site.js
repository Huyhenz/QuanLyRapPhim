// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener('DOMContentLoaded', () => {
    let currentIndex = 0;
    const items = document.querySelectorAll('.carousel-item');
    const totalItems = items.length;

    window.moveSlide = function (step) {
        // Xóa class 'active' khỏi tất cả các item
        items.forEach(item => item.classList.remove('active'));
        // Cập nhật chỉ số hiện tại
        currentIndex = (currentIndex + step + totalItems) % totalItems;
        // Thêm class 'active' cho item hiện tại
        items[currentIndex].classList.add('active');
    };

    // Tự động chuyển ảnh mỗi 5 giây
    setInterval(() => {
        moveSlide(1);
    }, 5000);
});