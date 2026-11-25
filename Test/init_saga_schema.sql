-- ==============================================================================
-- NpOn.Saga: Schema Thiết Kế Cơ Sở Dữ Liệu Phân Tán Không Khóa
-- Hỗ trợ Transactional Outbox, Inbox Causality Guard, DLQ & Binary Audit
-- ==============================================================================

-- 1. Bảng Outbox Có Khóa Xung Đột & Thứ Tự Đơn Điệu
CREATE TABLE IF NOT EXISTS saga_outbox (
    outbox_id BIGSERIAL PRIMARY KEY,
    saga_id UUID NOT NULL,
    entity_type VARCHAR(64) NOT NULL,
    entity_key VARCHAR(128) NOT NULL,
    pre_version BIGINT NOT NULL,
    post_version BIGINT NOT NULL,
    sequence_number BIGINT NOT NULL,
    event_type VARCHAR(128) NOT NULL,
    payload_json JSONB NOT NULL,
    status VARCHAR(16) NOT NULL DEFAULT 'NEW',
    retry_count INT NOT NULL DEFAULT 0,
    last_error TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    processed_at TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS idx_outbox_status ON saga_outbox(status);
CREATE INDEX IF NOT EXISTS idx_outbox_seq ON saga_outbox(entity_key, sequence_number);

-- 2. Bảng Inbox Giám Sát Khóa Phiên Bản (Causality Guard)
CREATE TABLE IF NOT EXISTS saga_inbox_entity_state (
    entity_type VARCHAR(64) NOT NULL,
    key_hash BIGINT NOT NULL,
    composite_key VARCHAR(128) NOT NULL,
    last_applied_version BIGINT NOT NULL,
    last_message_id VARCHAR(128) NOT NULL,
    is_quarantined BOOLEAN NOT NULL DEFAULT FALSE,
    retry_count INT NOT NULL DEFAULT 0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (entity_type, key_hash)
);
CREATE INDEX IF NOT EXISTS idx_inbox_key ON saga_inbox_entity_state(composite_key);

-- 3. Bảng Inbox Dead Letter Queue (DLQ / Poison Messages)
CREATE TABLE IF NOT EXISTS saga_inbox_dead_letter (
    dlq_id BIGSERIAL PRIMARY KEY,
    message_id VARCHAR(128) NOT NULL,
    entity_type VARCHAR(64) NOT NULL,
    key_hash BIGINT NOT NULL,
    composite_key VARCHAR(128) NOT NULL,
    failed_version BIGINT NOT NULL,
    payload_json JSONB NOT NULL,
    exception_type VARCHAR(256) NOT NULL,
    exception_message TEXT NOT NULL,
    retry_count INT NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'UNRESOLVED',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_dlq_status ON saga_inbox_dead_letter(status);
CREATE INDEX IF NOT EXISTS idx_dlq_entity ON saga_inbox_dead_letter(entity_type, composite_key);

-- 4. Bảng Audit Khóa Bản Ghi Tốc Độ Cao
CREATE TABLE IF NOT EXISTS saga_entity_audit (
    audit_id BIGSERIAL PRIMARY KEY,
    saga_id UUID NOT NULL,
    entity_type VARCHAR(64) NOT NULL,
    key_hash BIGINT NOT NULL,
    composite_key VARCHAR(128) NOT NULL,
    operation VARCHAR(16) NOT NULL,
    from_version BIGINT NOT NULL,
    to_version BIGINT NOT NULL,
    changed_fields_json JSONB NULL,
    actor_id VARCHAR(64) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_audit_key_ver ON saga_entity_audit(entity_type, key_hash, to_version);
CREATE INDEX IF NOT EXISTS idx_audit_created ON saga_entity_audit(created_at);

