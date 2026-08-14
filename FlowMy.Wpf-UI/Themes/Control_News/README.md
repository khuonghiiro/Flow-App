# Custom Control Styles (Control_News)

Thư mục này chứa các file `ResourceDictionary` (`.xaml`) định nghĩa Custom Styles riêng cho các control, views hoặc dialogs mới được thêm vào ứng dụng.

## Quy tắc thiết kế:
1. **Sử dụng Theme Tokens**: Tất cả các màu sắc, nền, viền, text PHẢI sử dụng `{DynamicResource TokenKey}` theo tài liệu `FlowMy.Docs/wpf-docs/THEME_TOKEN_REFERENCE.md`.
2. **Không hardcode màu sắc**: Tuyệt đối không dùng mã màu cố định như `#FF1A1B1E` hay `Red`, `Blue`.
3. **Phân tách XAML**: Giữ mỗi file `.xaml` dưới 800 dòng. Khi layout phình to, hãy phân rã thành các UserControl hoặc ResourceDictionary con.
