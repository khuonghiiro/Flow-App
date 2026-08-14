using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FlowMy.Helpers
{
    public static class MessageBoxHepler
    {
        public static void ShowMessageBoxHttp(System.Net.HttpStatusCode status, string? message = null)
        {
            var msg = string.IsNullOrWhiteSpace(message) ? string.Empty : " " + message;
            switch (status)
            {
                case System.Net.HttpStatusCode.OK: // 200
                    MessageBox.Show("Thao tác thành công." + msg, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;

                case System.Net.HttpStatusCode.Created: // 201
                    MessageBox.Show("Tạo mới bản ghi thành công." + msg, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;

                case System.Net.HttpStatusCode.NoContent: // 204
                    MessageBox.Show("Thao tác thành công (không có dữ liệu trả về)." + msg, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;

                case System.Net.HttpStatusCode.BadRequest: // 400
                    MessageBox.Show("Yêu cầu không hợp lệ (Bad Request)." + msg, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;

                case System.Net.HttpStatusCode.Unauthorized: // 401
                    MessageBox.Show("Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn." + msg, "Lỗi xác thực", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;

                case System.Net.HttpStatusCode.Forbidden: // 403
                    MessageBox.Show("Bạn không có quyền thực hiện hành động này." + msg, "Từ chối truy cập", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;

                case System.Net.HttpStatusCode.NotFound: // 404
                    MessageBox.Show("Không tìm thấy tài nguyên yêu cầu." + msg, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;

                case System.Net.HttpStatusCode.Conflict: // 409
                    MessageBox.Show("Xung đột dữ liệu. Vui lòng kiểm tra lại." + msg, "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;

                case System.Net.HttpStatusCode.InternalServerError: // 500
                    MessageBox.Show("Lỗi hệ thống (Internal Server Error) hoặc không có quyền.", "Lỗi server", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;

                case System.Net.HttpStatusCode.ServiceUnavailable: // 503
                    MessageBox.Show("Dịch vụ hiện không khả dụng. Vui lòng thử lại sau." + msg, "Lỗi server", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;

                default:
                    MessageBox.Show($"Lỗi không xác định (Mã: {(int)status})." + msg, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }

    }
}
