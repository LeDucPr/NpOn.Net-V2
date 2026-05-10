
CREATE TABLE IF NOT EXISTS system_log (
    created_at DateTime64(3),
    -- Cột ảo tự động chuyển đổi để tối ưu hóa việc quét dữ liệu theo ngày
    event_date Date MATERIALIZED toDate(created_at), 
    level LowCardinality(String),
    log_type Int16,
    source String,
    message String,
    
    -- Index phụ để tăng tốc khi lọc theo loại log (Log_Type)
    INDEX idx_log_type log_type TYPE minmax GRANULARITY 3
) ENGINE = MergeTree()
-- Chia thư mục vật lý theo tháng
PARTITION BY toYYYYMM(created_at)
-- Index chính: Ưu tiên tìm theo Source trước, sau đó đến Ngày
ORDER BY (source, event_date, created_at)
-- Cấu hình mặc định cho độ phân giải của index
SETTINGS index_granularity = 8192;

