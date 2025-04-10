using QuanLyRapPhim.Models.VNPay;

namespace QuanLyRapPhim.Service.VNPay
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(PaymentInformationModel model, HttpContext context);
        PaymentResponseModel PaymentExecute(IQueryCollection collections);

    }
}
