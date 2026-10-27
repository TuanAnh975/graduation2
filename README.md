# Hanoi Runtime Streets

Dự án Unity sinh phố Hà Nội trong runtime, có bộ tạo prefab nhà hình học 1–4 tầng. Đây là nền tảng kiến trúc thủ tục, chưa phải cảnh photorealistic hoàn thiện. Chưa được compile hoặc chạy bằng Unity trong môi trường cloud này vì máy không có Unity Editor/license.

## Mới: bản đồ rộng sinh quanh camera

1. Cập nhật mã trong `Assets/HanoiStreet` từ GitHub. Nếu đã có dự án làm việc, giữ lại `Generated`, các file `.meta` và scene/tài nguyên tự chỉnh sửa; không cần xóa Library hoặc tạo lại prefab nhà.
2. Mở Unity, chờ compile xong, chọn **Hanoi Street > Create open world scene**. Lưu scene đang làm trước khi chuyển.
3. Công cụ tạo scene **HanoiWorld** và profile **OpenWorld** trong `Assets/HanoiStreet/Generated`. Scene cũ **HanoiDemo** vẫn là tuyến phố thẳng để so sánh.
4. Nhấn **Play**, chờ các ô xung quanh được sinh dần trong vài giây. Nhấp tab **Game** để nhận điều khiển.
5. **W/A/S/D** di chuyển, **Q/E** xuống/lên, **Shift** tăng tốc; **giữ chuột phải** và rê chuột để nhìn. Thả chuột phải hoặc nhấn **Escape** để trả con trỏ; sau Escape cần thả rồi nhấn lại chuột phải. Camera bay tự do, có thể xuyên qua vật thể; chưa có nhân vật đi bộ/collision cho camera.

### Cấu trúc bản đồ

- Mạng đường hai chiều theo cả X/Z, các nút giao lệch vị trí theo seed và nối bằng đường cong Bézier. Đây là lưới đường biến dạng, chưa phải dữ liệu bản đồ Hà Nội thật hoặc hệ thống đường hữu cơ tùy ý.
- Mỗi ô sở hữu một nút giao và hai cạnh đi về phía đông/bắc; các cạnh còn lại do ô bên cạnh sở hữu. Điểm nối và tiếp tuyến lấy từ tọa độ toàn cục, không dựa vào thứ tự tải, nên các ô dùng chung cùng một nút đường.
- Mặt đường, vỉa hè, bó vỉa, dải phân cách trên một số trục chính, vạch đường và vạch sang đường được tạo theo đường cong. Nhà và các nhóm hàng quán/xe đỗ/cây tiếp tục dùng profile `Residential`.
- Một số nút có cầu vượt hướng đông–tây, đường bắc–nam nằm dưới; có đường dốc, trụ, vỉa hè và thành chắn. Không đặt nhà/hàng quán trên phần dốc. Đi xa dọc các đường để gặp cầu vượt; không phải mọi ô đều có cầu.
- Đèn có chu kỳ xanh/vàng/đỏ, hai hướng giao nhau không cùng xanh; các hướng tách cao độ không phải chờ nhau. Xe bám đường cong, có thể rẽ, chờ đèn và chờ ô kế tiếp tải xong; không rẽ từ cầu vượt xuống đường cắt bên dưới.
- Camera di chuyển làm tải các ô gần nhất trước và thu hồi ô ngoài vùng tải, gồm mesh tạo lúc chạy. Cùng seed/profile/prefab tái tạo cùng kiến trúc khi quay lại; trạng thái xe và đồng hồ tín hiệu không được lưu như trạng thái thế giới.
- Có floating origin: sau khoảng 8 ô, camera và các ô đang tải được dịch về gần gốc; tọa độ logic vẫn giữ nguyên. Điều này hỗ trợ đi xa, không phải cam kết thế giới vô hạn hay lưu game.

### Tinh chỉnh trước khi Play

Chọn `OpenWorld.asset` trong Project:

| Trường | Ý nghĩa |
| --- | --- |
| `seed` | Thay bố cục nút giao, nhà và cầu vượt |
| `cellSize` | Mặc định 256 m; tối thiểu 160 m; ô nhỏ làm dốc cầu gắt hơn |
| `loadRadius` | Mặc định 1 = tối đa 9 ô; 2 = 25 ô; 3 = 49 ô và có thể rất nặng |
| `junctionJitter` | Độ lệch nút giao, tăng để đường uốn rõ hơn |
| `roadWidth`, `sidewalkWidth` | Bề rộng đường và vỉa hè |
| `flyoverChance` | Xác suất cầu ở các nút đủ điều kiện; đặt 0 để tắt |
| `housesPerRoadSide` | Giới hạn số nhà mỗi bên một đoạn đường; tự giảm khi đường quá ngắn |
| `housesPerFrame` | Ngân sách số nhà sinh mỗi frame; giảm nếu xuất hiện khựng khi tải ô |
| `vehiclesPerRoad` | Mặc định 2; đặt 0 để tắt xe và kiểm tra bản đồ riêng |
| `streetAssets` | Profile chứa prefab nhà và vật thể đường phố |

Chỉnh thông số khi dừng Play, sau đó chạy lại. Không hỗ trợ thay kích thước ô/seed giữa lúc bản đồ đang tải. Khi đổi khoảng nhìn, kiểm tra thêm fog/camera trong scene. Bản mặc định dùng sương mù để làm dịu ranh giới vùng đang tải.

### Kiểm tra bản đồ mới

**Hanoi Street > Validate world graph** kiểm tra 338 cạnh đường trong vùng tọa độ âm/dương: điểm nối, tiếp tuyến, hình học khi dịch gốc và xung đột đèn xanh. Menu này phải chạy trong Unity; chưa được thực thi trong cloud.

Trong Play Mode, cần kiểm tra:

1. Đi qua nhiều ô theo cả X/Z và đường chéo; đường không đứt ở biên. Hierarchy chỉ giữ số ô trong phạm vi `loadRadius` sau khi đợt sinh kết thúc.
2. Quay về ô cũ: nhà, đường và cầu có cùng bố cục, không nhân đôi. Đứng sát biên và đi qua lại trong lúc ô còn đang sinh.
3. Bay nhanh hoặc đổi vị trí camera xa: ô cũ được thu hồi, ô mới sinh quanh camera; chấp nhận cảnh chưa kịp sinh trong lúc dịch chuyển quá nhanh.
4. Đi xa hơn 8 ô để kiểm tra floating origin: không thay đổi bố cục, xe/đèn vẫn hoạt động, camera không bật về vị trí cũ.
5. Kiểm tra vỉa hè, đường dốc và độ cao qua cầu, nhà không nằm trên dốc. Xe trên cầu không rẽ xuống tầng dưới.
6. Quan sát xe chờ đèn, rẽ và đi tiếp khi ô kế bên tải xong. Tắt/bật component streamer trong Play để kiểm tra thu hồi và sinh lại.
7. Dùng Unity Profiler trên máy đích để đo giật khung hình/bộ nhớ khi chạy lâu; chưa có số đo FPS hoặc cam kết hiệu năng.

Đây là bản streaming và giao thông minh họa: chưa có NavMesh, vật lý lái xe, tránh va chạm hoàn chỉnh, phân quyền rẽ, người đi bộ, LOD, object pooling hoặc save game. Xe có kiểm tra xe cùng hướng ở phía trước nhưng vẫn có thể chồng/cắt nhau tại giao lộ; xe thuộc ô đã bị thu hồi sẽ biến mất. Góc nút giao và mối nối vỉa hè hiện dùng các mảng hình học đơn giản, cần kiểm tra trực quan và tinh chỉnh. Chưa có xác nhận chạy Play Mode cho các chức năng mới trong cloud.

## Chạy dự án

1. Cài Unity **6000.0.23f1 (Unity 6 LTS)** qua Unity Hub, thêm thư mục repository làm project.
2. Chờ Package Manager cài URP 17.0.3; cần truy cập `packages.unity.com` và `download.packages.unity.com`.
3. Chọn **Hanoi Street > Create demo scene**. Công cụ tạo profile, 16 prefab nhà có chi tiết kiến trúc, các nhóm vật thể khác dạng khối mẫu, vật liệu, URP pipeline và scene trong `Assets/HanoiStreet/Generated`. Thư mục này được ignore; hãy chuyển tài nguyên muốn quản lý bằng Git sang thư mục khác.
4. Nhấn Play: phố được sinh từ seed trong profile `Residential`. Camera ban đầu đặt ở tầm mắt người trên đường. Dùng Scene view để quan sát toàn cảnh hoặc đặt lại camera trước Play.
5. Chỉnh profile rồi chạy lại, hoặc gọi menu ngữ cảnh **Generate street** trên component trong Play Mode. Cùng seed và cùng profile/prefab cho cùng bố cục. Thay seed để tạo phương án khác.

Mở lại scene đã tạo cho các lần tiếp theo. Menu tạo scene không ghi đè profile/prefab đã tồn tại, nhưng tạo lại scene demo sau khi hỏi lưu scene đang mở.

## Quy tắc hiện có

- Một tuyến phố thẳng hai chiều; `blocks` hiện chỉ quyết định tổng chiều dài, chưa tạo giao lộ hoặc mạng đường.
- Nhà ống liền kề, mặt tiền đều, chiều cao 1–4 tầng, hai bên đường. Prefab có `HanoiHouse` giữ nguyên chiều cao tầng và chiều sâu; chỉ scale chiều ngang theo lô đất.
- Hàng quán với bàn/ghế thành cụm; vị trí khác có xe máy đỗ hoặc cây. Đồ vật nằm về phía nhà, chừa một dải đi bộ gần đường.
- Thùng rác theo cụm; túi rác có xác suất xuất hiện cạnh thùng.
- Xe máy chiếm đa số; ô tô xuất hiện ít hơn. Xe chạy bên phải với tốc độ đồng đều, khoảng cách tối thiểu 8 m và vòng lại đầu tuyến. Đây là chuyển động minh họa, chưa có AI giao thông, nhường đường hay xử lý người đi bộ.
- Không dùng `UnityEngine.Random` toàn cục, nên sinh phố không làm thay đổi chuỗi ngẫu nhiên của hệ thống khác.

## Thay bằng prefab chân thật

Tạo prefab riêng cho nhà, cây, đường, vỉa hè, túi rác, thùng rác, bàn, ghế, xe máy, ô tô rồi gán vào mảng `variants` tương ứng. Mỗi prefab dùng đơn vị mét, pivot tại tâm đáy, trục +Y lên trên, +Z hướng mặt trước. `nominalSize` là kích thước bao chuẩn theo X/Y/Z của cả nhóm; chuẩn hóa variant về cùng kích thước trước khi gán. Prefab nhà có X là chiều ngang mặt tiền, Z là chiều sâu. Generator xoay mặt nhà vào đường. Tránh script tự di chuyển trên prefab giao thông vì generator đã thêm `StreetVehicle`.

Nhà tùy chỉnh có thể thêm component `HanoiHouse` và khai báo `bodySize` để giữ chiều cao/chiều sâu. Prefab không có component này được scale theo kích thước lô. Các khối mẫu khác chỉ thể hiện vị trí/kích thước, chưa thể hiện hình dáng thực của xe, cây hay bàn ghế.

## Bộ prefab nhà Hà Nội

**Hanoi Street > Create house prefab library** tạo riêng bộ 16 biến thể: 1, 2, 3, 4 tầng × cửa cuốn/cửa kính × chuồng cọp mái tôn/mái ngói. Prefab được tạo khi chạy công cụ trong Unity Editor, không phải khi tải repository về; chưa có file prefab xuất sẵn được kiểm chứng trong cloud.

- Tầng trệt: cửa cuốn có nan hoặc hai cánh kính màu có tay nắm, biển quảng cáo tiếng Việt, mái che cuốn dạng triển khai với con lăn, tay đỡ và vải sọc. Mái che chưa có animation đóng/mở.
- Tầng 2 trở lên: cửa sổ có khung, sàn ban công, lan can thép và cờ Việt Nam tỉ lệ 3:2 với ngôi sao vàng năm cánh. Các tầng trên lặp lại module này.
- Mái: sân thượng quây khung sắt chuồng cọp và mái tôn dốc có gân, hoặc mái ngói hai dốc màu đất nung có hàng ngói và nóc mái.
- Nhà một tầng chỉ có tầng trệt và mái; không tự thêm tầng ban công.
- Mặt tiền rộng 4,5 m, thân nhà sâu 8 m, mỗi tầng cao 3,4 m. Mái, ban công và mái che nằm ngoài kích thước thân nhà.
- Mesh kiến trúc được gộp theo vật liệu, lưu thành asset và dùng chung giữa các instance. Collider hiện chỉ bao thân nhà; ban công, mái che và chuồng cọp chưa có collider riêng.

Menu **Create demo scene** tự gán bộ nhà nếu profile mới hoặc còn dùng prefab `House_BLOCKOUT` cũ. Profile đã gán nhà tùy chỉnh được giữ nguyên. Nếu chỉ tạo library riêng, chọn profile `Residential` rồi kéo các prefab vào `houses.variants`.

Các lần tạo sau giữ nguyên prefab/vật liệu đã tồn tại. Muốn tạo lại mà vẫn giữ bản chỉnh sửa, chuyển cả thư mục `Generated/Houses` cũ ra vị trí khác trước. Chạy **Hanoi Street > Validate house prefab library** để kiểm tra đủ 16 tổ hợp, chiều cao tầng, biển, cờ, vật liệu và mesh lưu trên đĩa. Kiểm tra này chưa được chạy trong cloud và không thay thế kiểm tra hình ảnh trong Play Mode.

Vật liệu hiện dùng màu phẳng, kính màu đục để tránh lỗi sắp xếp trong suốt; chưa có nội thất, texture PBR, cửa có hốc thật, tường bong tróc, animation cờ, hay ngói rời chi tiết. Biển dùng font có sẵn của Unity; cần kiểm tra dấu tiếng Việt trên máy và thay font có giấy phép phù hợp nếu thiếu glyph.

## Hướng đạt hình ảnh giống Hà Nội

Ưu tiên tài nguyên có giấy phép sử dụng rõ ràng: nhiều biến thể nhà ống có tầng trệt kinh doanh, cửa cuốn, ban công, mái che, biển tiếng Việt; xe máy phổ biến; ghế nhựa thấp; gạch vỉa hè và mép bó vỉa. Dùng ảnh tham chiếu của một tuyến phố cụ thể để thống nhất bề rộng, mật độ và kiến trúc. Bộ model/vật liệu là phần việc còn thiếu của bản này.

Vật liệu cần albedo/normal/roughness phù hợp URP, tỉ lệ texel nhất quán, vết bẩn và ẩm ở vị trí có nguyên nhân. Thêm ánh sáng bầu trời/HDRI, exposure và color grading sau khi có asset thật. Không thể đạt photorealism chỉ bằng tăng số lượng vật thể ngẫu nhiên.

Giai đoạn tiếp theo: prefab facade module, mái che và biển hiệu theo loại hàng; dây điện/cột điện; lối vào và ràng buộc tránh chắn cửa; người đi bộ và hành vi xe máy đầy đủ; decal; LOD, GPU instancing và object pooling. Bản đồ mới đã có giao lộ và streaming theo ô, nhưng vẫn cần kiểm chứng trực quan/hiệu năng trong Unity.

## Kiểm chứng trong Unity cần thực hiện

- Console không có lỗi compile, shader hoặc renderer khi tạo demo và Play.
- Cùng seed tạo cùng tọa độ/variant; seed khác thay đổi cảnh.
- Nhà quay mặt vào đường; xe đi bên phải, không chồng lên nhau sau khi vòng tuyến.
- Gọi Generate nhiều lần chỉ còn một nhóm phố đang hoạt động.
- Thử profile 1 và 8 blocks; kiểm tra đầu/cuối tuyến, dải đi bộ và phạm vi camera.
- Thay ít nhất một prefab thật ở mỗi nhóm, kiểm tra pivot, scale, hướng và collider.

Không có kết quả Play Mode hay đánh giá hình ảnh được xác nhận trong cloud. Đây là các bước kiểm tra bắt buộc trước khi coi demo chạy ổn.
