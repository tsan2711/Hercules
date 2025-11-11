# Chess Bot AI - Hướng dẫn sử dụng

## Tổng quan
ChessBotAI là một AI bot thông minh cho game cờ vua, sử dụng thuật toán Minimax với Alpha-Beta Pruning và nhiều kỹ thuật tối ưu khác.

## Cấu hình Bot

### 1. Bot Settings (Cài đặt cơ bản)
- **isLevelMode**: Chế độ level (bot tự động chơi khi đến lượt đen)
- **currentLevel**: Level hiện tại (1-10+), tự động parse từ scene name
- **botMoveDelay**: Thời gian delay trước khi bot đánh (giây)

### 2. Difficulty Settings (Độ khó)

#### Minimax Depth (Độ sâu tìm kiếm)
- **minMaxDepthEasy** (1): Level 1-3 - Bot suy nghĩ 1 nước trước
- **minMaxDepthMedium** (2): Level 4-6 - Bot suy nghĩ 2 nước trước  
- **minMaxDepthHard** (3): Level 7-9 - Bot suy nghĩ 3 nước trước
- **minMaxDepthExpert** (4): Level 10+ - Bot suy nghĩ 4 nước trước

> **Lưu ý**: Depth càng cao, bot càng thông minh nhưng tốn thời gian hơn. Khuyến nghị không vượt quá 5.

#### Các tham số khác
- **quiescenceDepth** (2): Độ sâu tìm kiếm bổ sung cho các nước đi chiến thuật
- **maxQuiescenceDepth** (3): Giới hạn tối đa cho quiescence search
- **maxThinkingTime** (5s): Thời gian suy nghĩ tối đa cho mỗi nước
- **maxMovesPerDepth** (30): Số lượng nước đi được xem xét (nhiều hơn = thông minh hơn)
- **useMinimaxForAllLevels** (true): Dùng Minimax cho tất cả level (trừ level 1-2)

## Chiến lược theo Level

### Level 1-3 (Difficulty 1) - RANDOM
- Bot đi ngẫu nhiên
- Phù hợp cho người mới bắt đầu
- Không có chiến thuật

### Level 4-6 (Difficulty 2) - GREEDY hoặc MINIMAX Medium
- Nếu `useMinimaxForAllLevels = false`: Dùng Greedy (ưu tiên ăn quân)
- Nếu `useMinimaxForAllLevels = true`: Dùng Minimax depth 2
- Bắt đầu có chiến thuật cơ bản

### Level 7-9 (Difficulty 3) - MINIMAX Hard
- Minimax với depth 3
- Xem xét check, checkmate, fork, pin
- Chiến thuật nâng cao

### Level 10+ (Difficulty 4) - MINIMAX Expert
- Minimax với depth 4
- Chiến thuật cao cấp
- Rất khó đánh bại

## Hệ thống đánh giá (Evaluation System)

### 1. Giá trị quân cờ
- Pawn (Tốt): 10
- Knight (Mã): 30
- Bishop (Tượng): 30
- Rook (Xe): 50
- Queen (Hậu): 90
- King (Vua): 900

### 2. Piece-Square Tables
Bot đánh giá vị trí tốt cho từng loại quân:
- **Pawn**: Khuyến khích tiến lên, kiểm soát trung tâm
- **Knight**: Tốt nhất ở trung tâm, tránh góc
- **Bishop**: Ưu tiên đường chéo dài
- **Rook**: Tốt ở hàng 7 và cột mở
- **Queen**: Linh hoạt, ở trung tâm
- **King**: An toàn ở góc (opening), trung tâm (endgame)

### 3. Các yếu tố chiến thuật
- **Check/Checkmate**: Bonus cực lớn (10000 cho checkmate)
- **Capture**: Đánh giá giá trị trao đổi (MVV-LVA)
- **Mobility**: Số nước đi có thể
- **Center Control**: Kiểm soát trung tâm
- **King Safety**: Bảo vệ vua
- **Pawn Structure**: Cấu trúc tốt

## Tối ưu hóa Performance

### Các kỹ thuật đã sử dụng:
1. **Alpha-Beta Pruning**: Cắt tỉa các nhánh không cần thiết
2. **Move Ordering**: Sắp xếp moves theo độ ưu tiên (captures > checks > others)
3. **Quiescence Search**: Tìm kiếm sâu thêm trong các tình huống chiến thuật
4. **Timeout Protection**: Dừng tính toán nếu quá thời gian
5. **Coroutine**: Không block Unity Editor

### Lưu ý khi điều chỉnh:
- Depth > 4: Rất chậm, chỉ dùng cho high-end device
- maxMovesPerDepth > 50: Tốn thời gian, ít cải thiện chất lượng
- maxThinkingTime < 3s: Bot có thể không kịp suy nghĩ

## Debug & Monitoring

Bot sẽ log các thông tin:
```
[ChessBotAI] Using MINIMAX Hard strategy with depth 3
[ChessBotAI] Evaluating 30 out of 45 moves
[ChessBotAI] New best move: Knight from (1, 7) to (2, 5), score: 150
[ChessBotAI] Finished evaluation: 30 moves, best score: 150
```

## Troubleshooting

### Bot chỉ di chuyển 1 quân
- Đảm bảo `useMinimaxForAllLevels = true`
- Kiểm tra `currentLevel` đúng (>= 3 để dùng Minimax)
- Xem debug logs để biết chiến lược đang dùng

### Bot đi chậm
- Giảm `minMaxDepth`
- Giảm `maxMovesPerDepth`
- Giảm `maxThinkingTime`

### Bot đi quá nhanh/yếu
- Tăng `minMaxDepth`
- Tăng `maxMovesPerDepth`
- Bật `useMinimaxForAllLevels`

### Bot không đi
- Kiểm tra `isLevelMode = true`
- Kiểm tra scene name có format "Level_X"
- Xem Console có lỗi không

## Khuyến nghị cấu hình

### ⚡ MẶC ĐỊNH (Đã tối ưu - KHUYẾN NGHỊ):
```
minMaxDepthEasy = 1
minMaxDepthMedium = 2
minMaxDepthHard = 2
minMaxDepthExpert = 3
quiescenceDepth = 1
maxThinkingTime = 2s
maxMovesPerDepth = 20
yieldEveryNMoves = 1
useMinimaxForAllLevels = true
```
**Lý do**: Cân bằng giữa hiệu suất và độ thông minh, không lag editor

### 📱 Cho Mobile (Tối ưu performance):
```
minMaxDepthEasy = 1
minMaxDepthMedium = 1
minMaxDepthHard = 2
minMaxDepthExpert = 2
quiescenceDepth = 0
maxThinkingTime = 1.5s
maxMovesPerDepth = 15
yieldEveryNMoves = 1
```

### 💻 Cho PC mạnh:
```
minMaxDepthEasy = 1
minMaxDepthMedium = 2
minMaxDepthHard = 3
minMaxDepthExpert = 3
quiescenceDepth = 1
maxThinkingTime = 3s
maxMovesPerDepth = 25
yieldEveryNMoves = 1
```

### 🏆 Cho Tournament (Chất lượng cao - CÓ THỂ LAG):
```
minMaxDepthEasy = 2
minMaxDepthMedium = 2
minMaxDepthHard = 3
minMaxDepthExpert = 4
quiescenceDepth = 1
maxThinkingTime = 5s
maxMovesPerDepth = 30
yieldEveryNMoves = 1
```
**Cảnh báo**: Depth 4 có thể gây lag! Chỉ dùng nếu chấp nhận chờ lâu.

## 🚨 Cảnh báo Performance

### Nguyên nhân gây lag:
1. **Depth quá cao** (>3): Số lượng tính toán tăng theo cấp số nhân
2. **maxMovesPerDepth quá lớn** (>30): Quá nhiều moves được xem xét
3. **Quiescence search** (đã TẮT mặc định): Gây đệ quy vô hạn
4. **Complex evaluation** (đã TẮT): GetMobility, King safety... rất chậm

### Đã tối ưu:
- ✅ Yield sau mỗi move để không block editor
- ✅ Timeout protection (80% maxThinkingTime)
- ✅ Tắt Quiescence search
- ✅ Đơn giản hóa EvaluateBoard (chỉ tính piece value + position)
- ✅ Đơn giản hóa OrderMoves (không check, chỉ sort captures)
- ✅ Giảm moves xem xét ở depth sâu (10 moves thay vì maxMovesPerDepth)
- ✅ Alpha-beta pruning để cắt tỉa nhánh

### Giám sát performance:
```
[ChessBotAI] Performance: maxThinkingTime=2s, maxMoves=20, yield every 1 moves
[ChessBotAI] Finished evaluation: 15 moves in 0.45s, best score: 250
```

Nếu thấy `Evaluation took X.XXs!` > 1s, hãy giảm depth hoặc maxMovesPerDepth!

