--
-- PostgreSQL database dump
--

\restrict YrBjzLapw78g6f5ymOxwIzSmdccPfC5uV9rbGVrDJMkH4IXNGlweYy4hDPfMyMe

-- Dumped from database version 18.3 (Debian 18.3-1.pgdg13+1)
-- Dumped by pg_dump version 18.3 (Ubuntu 18.3-1.pgdg24.04+1)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: public; Type: SCHEMA; Schema: -; Owner: pg_database_owner
--

CREATE SCHEMA public;


ALTER SCHEMA public OWNER TO pg_database_owner;

--
-- Name: SCHEMA public; Type: COMMENT; Schema: -; Owner: pg_database_owner
--

COMMENT ON SCHEMA public IS 'standard public schema';


--
-- Name: ensure_locale_partitions(character varying[]); Type: PROCEDURE; Schema: public; Owner: postgres
--

CREATE PROCEDURE public.ensure_locale_partitions(IN lang_codes character varying[])
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_code varchar;
    v_partition_name text;
BEGIN
    FOREACH v_code IN ARRAY lang_codes LOOP
        v_partition_name := 'mlg_srv_locale_' || lower(v_code);

        IF to_regclass('public.' || v_partition_name) IS NULL THEN
            EXECUTE format(
                'CREATE TABLE %I PARTITION OF mlg_srv_locale FOR VALUES IN (%L)',
                v_partition_name,
                v_code
            );

            EXECUTE format(
                'CREATE UNIQUE INDEX %I ON %I (mlg_srv_country_lang_code, resource_key)',
                'ux_' || v_partition_name || '_lang_resource',
                v_partition_name
            );
        END IF;
    END LOOP;
END;
$$;


ALTER PROCEDURE public.ensure_locale_partitions(IN lang_codes character varying[]) OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: fld_query_master; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.fld_query_master (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    description character varying(800),
    tblmaster_id uuid NOT NULL,
    field_name character varying(700) NOT NULL,
    field_type integer,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    field_type_string character varying(50),
    order_sort integer DEFAULT 0
);


ALTER TABLE public.fld_query_master OWNER TO postgres;

--
-- Name: formula_config; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.formula_config (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    table_name character varying(255),
    table_column character varying(255),
    code character varying(510) NOT NULL,
    prefix text,
    current_value text,
    suffix text,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone,
    formula_id uuid NOT NULL
);


ALTER TABLE public.formula_config OWNER TO postgres;

--
-- Name: generic_formula; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.generic_formula (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    fomula_name text,
    block_type integer,
    data_type integer,
    start_value_text text,
    end_value_text text,
    current_value_text text,
    regex_text text DEFAULT ''::text NOT NULL,
    components text,
    logic_type integer
);


ALTER TABLE public.generic_formula OWNER TO postgres;

--
-- Name: tblmaster; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.tblmaster (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    code character varying(800) NOT NULL,
    description character varying(800),
    execfunc character varying(1000),
    query text,
    exectype integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    service_name character varying(100),
    db_type integer
);


ALTER TABLE public.tblmaster OWNER TO postgres;

--
-- Name: vw_query_master; Type: VIEW; Schema: public; Owner: postgres
--

CREATE VIEW public.vw_query_master AS
 SELECT tbl.id AS tbl_id,
    tbl.code AS tbl_code,
    tbl.description AS query_desc,
    tbl.execfunc AS exec_func,
    tbl.query,
    tbl.exectype AS exec_type,
    tbl.service_name,
    tbl.db_type,
    fld.id AS fld_id,
    fld.order_sort,
    fld.field_name,
    fld.field_type,
    fld.field_type_string
   FROM (public.tblmaster tbl
     LEFT JOIN public.fld_query_master fld ON ((fld.tblmaster_id = tbl.id)));


ALTER VIEW public.vw_query_master OWNER TO postgres;

--
-- Data for Name: fld_query_master; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.fld_query_master (id, description, tblmaster_id, field_name, field_type, created_at, field_type_string, order_sort) FROM stdin;
d7b2a660-1e4c-4984-82e9-1bbe17609c43	Parameter @account_ids	49c95e57-13ba-4cba-ac25-ed156f31c377	account_ids	-2147483621	2025-12-26 09:22:32.462895+07	\N	0
ffd48f44-b6a3-4ce2-9818-180a80a5ce78	Parameter @ids	28d85abe-c35d-4d14-bfed-1363e5470109	ids	-2147483621	2026-02-09 03:04:21.484682+07	\N	0
04c52184-140e-4e7a-a08c-e06ea314c68c	Parameter @codes	128998eb-002b-4213-88c6-e5f3072be803	codes	-2147483626	2026-02-09 03:34:45.215508+07	\N	0
b54ade4b-0030-44fd-b57b-1fd736064e1c	Parameter @account_id	f2499199-ebf8-4002-8095-c4ff86c1f50a	account_id	27	2026-02-09 04:15:24.775635+07	NpgsqlDbType.Uuid	0
dc3a0d9d-88bb-48be-9c6f-29d703f25f96	ID Khảo sát	8c18f93d-629e-4d4c-a77c-3f555c9574a6	id	27	2025-11-20 23:29:03.30255+07	NpgsqlDbType.Uuid	0
183507cd-2b6b-49fa-8d32-d503f1a15280	\N	f092152c-5fe0-4315-873d-e247e72020b9	survey_id	27	2025-11-20 21:27:42.000614+07	NpgsqlDbType.Uuid	0
04a233c0-eeab-4c66-8c45-81a03202006d	Tiêu đề	8c18f93d-629e-4d4c-a77c-3f555c9574a6	title	19	2025-11-20 23:29:03.30255+07	NpgsqlDbType.Text	0
2c2db9f5-80eb-475a-b89b-e43b9a426956	Mô tả	8c18f93d-629e-4d4c-a77c-3f555c9574a6	description	19	2025-11-20 23:29:03.30255+07	NpgsqlDbType.Text	0
335802f5-33d1-4b9a-a80c-019af2bfe396	Ngày hết hạn	8c18f93d-629e-4d4c-a77c-3f555c9574a6	expired_at	26	2025-11-20 23:29:03.30255+07	NpgsqlDbType.TimestampTz	0
440e1da0-c89f-45ab-8385-4e0ed89fca31	Ngày tạo	8c18f93d-629e-4d4c-a77c-3f555c9574a6	created_at	26	2025-11-20 23:29:03.30255+07	NpgsqlDbType.TimestampTz	0
76f3820d-da92-44ce-bc96-d03923c944fc	ID người làm bài	82cbb8ca-23b5-4d4a-8151-a0f05a7844cc	user_id	27	2025-11-25 00:09:24.991156+07	NpgsqlDbType.Uuid	0
497b74d6-3b7f-4373-839f-471c18ec2bee	ID câu hỏi	82cbb8ca-23b5-4d4a-8151-a0f05a7844cc	question_id	27	2025-11-25 00:09:24.991156+07	NpgsqlDbType.Uuid	0
2e9727e8-c67d-42fa-8c3b-6c14f397c315	Tiêu đề	a77aaa63-dc1e-48b0-9409-5445d2c1e0e6	title	22	2025-11-24 22:23:03.172344+07	NpgsqlDbType.Varchar	0
14d9d0dd-f909-4a15-91cc-565e56901dd9	Mô tả	a77aaa63-dc1e-48b0-9409-5445d2c1e0e6	description	19	2025-11-24 22:23:03.131442+07	NpgsqlDbType.Text	0
a16ea1f6-273a-42a7-b72b-327f3c504062	Publish?	a77aaa63-dc1e-48b0-9409-5445d2c1e0e6	is_published	2	2025-11-24 22:23:03.103784+07	NpgsqlDbType.Boolean	0
6242ea5b-ecc4-4492-8e70-5091af0a9006	SurveyId cần cập nhật	a77aaa63-dc1e-48b0-9409-5445d2c1e0e6	id	27	2025-11-24 22:23:03.001785+07	NpgsqlDbType.Uuid	0
0ac44eae-ed91-41bc-91c3-c9b05e8a83d8	Thời điểm hết hạn bộ câu hỏi	a77aaa63-dc1e-48b0-9409-5445d2c1e0e6	expired_at	26	2025-11-24 22:23:02.791059+07	NpgsqlDbType.TimestampTz	0
a78f1e6e-bdda-4e0f-8ce8-c15f550d5497	Thời điểm hết hạn bộ câu hỏi	a3c0026e-23ae-44a5-ae88-787a3ed44052	expired_at	26	2025-11-24 20:58:23.683128+07	NpgsqlDbType.TimestampTz	0
2293dc70-d797-4fe9-a2b8-374ab9f95398	Publish?	a3c0026e-23ae-44a5-ae88-787a3ed44052	is_published	2	2025-11-24 20:58:10.710177+07	NpgsqlDbType.Boolean	0
06b6d5b4-ec2a-474d-a365-bfa4cec383f0	Mô tả	a3c0026e-23ae-44a5-ae88-787a3ed44052	description	19	2025-11-24 20:58:10.682975+07	NpgsqlDbType.Text	0
2e8fa760-fb4b-45b1-84cc-405581113444	Tiêu đề	a3c0026e-23ae-44a5-ae88-787a3ed44052	title	22	2025-11-24 20:58:10.654222+07	NpgsqlDbType.Varchar	0
4eeda651-4136-40c6-9c44-e371d6796dfb	\N	fecf2920-4633-469f-a6af-bdf06149b58a	json_object_data	35	2025-11-24 17:27:03.934182+07	NpgsqlDbType.Json	0
e5474881-4373-4781-8ac4-d4b46959dc76	Parameter @id	f4865b1d-1824-497e-8f43-b1888335e888	id	27	2025-12-30 09:15:21.741436+07	NpgsqlDbType.Uuid	0
75d51fac-f0dd-45ab-9943-1acd8ff96df1	Parameter @parent_id	def96aa8-83c1-4b97-9c26-913302f9e2fd	parent_id	27	2025-12-31 10:16:20.143927+07	NpgsqlDbType.Uuid	0
bfd104d5-b98b-480f-91ab-40223bb9225e	Câu trả lời tự luận (nếu có)	82cbb8ca-23b5-4d4a-8151-a0f05a7844cc	text_answer	19	2025-11-25 00:09:24.991156+07	NpgsqlDbType.Text	0
a5803950-f11e-4cde-a60a-4bc9936b336e	Mảng ID các đáp án đã chọn	82cbb8ca-23b5-4d4a-8151-a0f05a7844cc	answer_ids	-2147483621	2025-11-25 00:09:24.991156+07	\N	0
5592e5b9-deb2-4d6a-9143-fe83250f0a69	ID người dùng cần tính điểm	06bbdd03-e15e-4e66-8c3a-586469446a59	user_id	27	2025-11-25 00:09:24.991156+07	NpgsqlDbType.Uuid	1
9b5a1790-5a07-4da3-8e93-f76cf8996920	ID bài khảo sát	06bbdd03-e15e-4e66-8c3a-586469446a59	survey_id	27	2025-11-25 00:09:24.991156+07	NpgsqlDbType.Uuid	2
2dfbdb29-f9ad-468e-98d4-9ea82f615800	Mã khảo sát	caa66f91-075f-4297-a5eb-088d39e4a2cc	ques_srv_survey_id	27	2025-11-26 15:03:48.77627+07	NpgsqlDbType.Uuid	0
7b0bb230-3cc1-45f4-b416-a67b3c67553d	Tổng điểm khảo sát	caa66f91-075f-4297-a5eb-088d39e4a2cc	total_score	9	2025-11-26 15:03:48.849016+07	NpgsqlDbType.Integer	0
a8a17643-9a06-4468-a290-537a3f124ad4	UserName	b3a30e8e-8aad-4894-8c6e-d824f8d692c6	username	22	2025-11-27 15:45:18.072603+07	NpgsqlDbType.Varchar	0
db639886-fa76-4ed7-8feb-af4e1d350ea9	Password	b3a30e8e-8aad-4894-8c6e-d824f8d692c6	password	22	2025-11-27 15:45:18.102107+07	NpgsqlDbType.Varchar	0
c6dc8df4-c4f4-4df8-a38d-55850101cf15	RefreshToken	f31f7385-4dd0-412e-921d-bf1bde1bfe05	refresh_token	22	2025-11-27 23:07:44.723373+07	NpgsqlDbType.Varchar	0
6c612ce4-2a3b-44dc-8a81-211c9dce5239	SessionId	4583025a-5dfd-428b-89a1-a3451ef6984d	session_id	22	2025-11-27 23:25:44.486501+07	NpgsqlDbType.Varchar	0
06eaa7c1-36b2-4edb-b4d9-f5c68a5048f9	AccountId	d2b894c4-ccb6-4437-af9e-4254f063dac6	account_id	27	2025-11-27 23:12:42.063689+07	NpgsqlDbType.Uuid	0
34811748-7815-466f-8174-0fb55d0e3119	\N	93099ec1-23bc-49cf-9083-1638f215a4ab	json_object_data	35	2025-11-28 14:46:04.871426+07	NpgsqlDbType.Json	0
3bd8114c-2f99-43f2-a991-58e6c768f96d	ID của bảng kết quả survey	82cbb8ca-23b5-4d4a-8151-a0f05a7844cc	result_id	27	2025-11-29 22:58:59.826473+07	NpgsqlDbType.Uuid	0
daca6485-e136-4944-b653-d69da86c50e5	ID của bài khảo sát	66a5c83d-6d95-4ca1-8b9a-10e56d1a69db	survey_id	27	2025-11-29 22:59:27.314048+07	NpgsqlDbType.Uuid	0
29b15d58-51f0-430c-8f45-326e4c4ca731	Id (bảng AccountId)	3b17d2aa-07e2-4ec6-a204-b587b504889a	id	27	2025-11-28 17:02:28.998624+07	NpgsqlDbType.Uuid	0
d341e8c1-68d1-410b-82b2-e23d9df9f455	Mảng ID các câu trả lời	4d599b40-2e94-40f6-881f-289d890b7076	answer_ids	-2147483621	2025-11-29 22:59:27.314048+07	\N	0
4f33b9fd-5b60-4636-80df-6da58e7530e7	ID người dùng	5b11c553-a2f1-4a55-8c6f-9103f31fa7f1	user_id	27	2025-11-29 22:59:27.314048+07	NpgsqlDbType.Uuid	0
7ec41e5c-8a8f-4bdf-9081-38bb2b730f6a	ID bài khảo sát	5b11c553-a2f1-4a55-8c6f-9103f31fa7f1	survey_id	27	2025-11-29 22:59:27.314048+07	NpgsqlDbType.Uuid	0
2c5c33fa-d3c3-4b37-805c-4ca257baf138	Tổng điểm đạt được	5b11c553-a2f1-4a55-8c6f-9103f31fa7f1	total_score	9	2025-11-29 22:59:27.314048+07	NpgsqlDbType.Integer	0
c48977b3-37f3-49f7-a538-d71493bd5fda	Điểm tối đa	5b11c553-a2f1-4a55-8c6f-9103f31fa7f1	max_score	9	2025-11-29 22:59:27.314048+07	NpgsqlDbType.Integer	0
737e809d-e16f-4a7b-98ee-fd1d49268c89	Parameter @parent_dealer_id	702c6f43-1480-45fe-933c-ee6bd3874c2d	parent_dealer_id	27	2026-01-02 09:19:13.135499+07	NpgsqlDbType.Uuid	0
6d448f86-e038-4d96-87cd-d8a30b05f9d7	Parameter @dealer_level_id	702c6f43-1480-45fe-933c-ee6bd3874c2d	dealer_level_id	27	2026-01-02 09:19:13.200341+07	NpgsqlDbType.Uuid	1
4391452c-7d4f-4dbf-aea4-aa15bb9d9a72	Parameter @keyword	702c6f43-1480-45fe-933c-ee6bd3874c2d	keyword	19	2026-01-02 09:19:13.204792+07	NpgsqlDbType.Text	2
2e85aac8-6f3a-455d-80fa-ef0856b0da98	Parameter @ids	9d880502-f2ad-41da-aa51-ac0c4ab00061	ids	-2147483621	2026-01-03 06:29:17.206344+07	\N	0
384b1ca5-1200-4b0f-a938-a91915ae1b7e	Parameter @product_ids	a4525bf6-d6e3-41ca-b8f2-b0b4f07bc843	product_ids	-2147483621	2026-01-03 06:30:24.589914+07	\N	0
8002fb79-2f64-4b77-bff1-4c1fbef3374e	Parameter @ids	61c8e7ea-79e8-40d1-8d4c-f00eb81712fe	ids	-2147483621	2026-01-03 06:57:20.944528+07	\N	0
5af157e5-8311-4a43-a29e-1fbd7ac921d1	Parameter @product_parent_ids	89b6b21d-f6c3-4dc2-8929-813df27c2ead	product_parent_ids	-2147483621	2026-01-03 07:23:01.169609+07	\N	0
ce1a046f-a18c-450a-8f4e-af70ebb6428e	Parameter @account_ids	61ea9946-695c-464b-8c5c-9e36201e1bc7	account_ids	-2147483621	2026-01-06 03:06:35.567938+07	\N	0
49890e02-1124-4c80-b3ad-0f546d5b15a4	Parameter @dealer_ids	6d3f46eb-599d-43b2-98d8-8b0da07bcb4c	dealer_ids	-2147483621	2026-01-06 03:07:17.349+07	\N	0
308f6a7b-8357-4452-8220-f2629ff29aba	Parameter @keyword	38e4d433-92da-4b5d-a5b1-a3bb0e5174bc	keyword	19	2026-01-06 08:13:26.928355+07	NpgsqlDbType.Text	1
25619619-49f5-47d7-bf4d-bcabc3e7280f	Parameter @page_size	38e4d433-92da-4b5d-a5b1-a3bb0e5174bc	page_size	9	2026-01-06 08:13:26.928444+07	NpgsqlDbType.Integer	2
e857f62c-1322-4127-920f-380db9d95286	Parameter @page_index	38e4d433-92da-4b5d-a5b1-a3bb0e5174bc	page_index	9	2026-01-06 08:13:26.928526+07	NpgsqlDbType.Integer	3
21efcf51-f6f7-4b94-9eea-e6dcf0e24da2	Parameter @unit_group	38e4d433-92da-4b5d-a5b1-a3bb0e5174bc	unit_group	9	2026-01-06 08:13:26.928246+07	NpgsqlDbType.Integer	0
5e2b3185-4eed-4242-9fd5-191d0458d94c	Parameter @id	fb626783-7e46-48fa-971d-c5e70d86ca0a	id	27	2026-01-06 10:46:06.775046+07	NpgsqlDbType.Uuid	1
43cdf8d8-e9c9-404a-8c7e-dc67c1a6c626	Parameter @page_index	702c6f43-1480-45fe-933c-ee6bd3874c2d	page_index	9	2026-01-02 09:19:13.205238+07	NpgsqlDbType.Integer	4
5b54d888-a602-4db5-90a0-404eab40a07c	Parameter @page_size	702c6f43-1480-45fe-933c-ee6bd3874c2d	page_size	9	2026-01-02 09:19:13.205003+07	NpgsqlDbType.Integer	3
0f736b6b-fce6-4583-b125-ba68ce5090e5	Parameter @product_id	f5651437-15b7-419d-b3d5-5ea1b3f29126	product_id	27	2026-01-07 05:04:44.641391+07	NpgsqlDbType.Uuid	0
86ee10e5-334b-4938-964a-97498616ce2c	Parameter @ids	9c137cec-6418-466f-a1c0-c6778b90a7a6	ids	-2147483621	2026-01-15 05:48:58.547198+07	\N	0
6ee9d8a3-37be-4dfa-8ee9-9213092831af	Dữ liệu outcome dạng JSON	5b11c553-a2f1-4a55-8c6f-9103f31fa7f1	outcome_data	35	2025-11-29 22:59:27.314048+07	NpgsqlDbType.Jsonb	0
f41f37b1-efea-4954-a21d-331974c0a8d1	Email	2413277c-6254-41aa-86fd-18bc6c2e79b2	email	22	2025-12-04 14:46:57.932398+07	NpgsqlDbType.Varchar	0
fcf5d4d0-b69e-42cf-9a68-eeafe902d581	UserName	2413277c-6254-41aa-86fd-18bc6c2e79b2	username	22	2025-12-04 14:46:57.984399+07	NpgsqlDbType.Varchar	0
7d3c33c6-32a7-45ce-926e-119c5bff8c16	PhoneNumber	2413277c-6254-41aa-86fd-18bc6c2e79b2	phone_number	22	2025-12-04 14:46:57.958952+07	NpgsqlDbType.Varchar	0
dd833ff7-74d9-4a66-96ea-da56ef4155b8	Dynamic Json Query	34b687b8-f94a-4101-a5f5-4f4640d112d3	json_object_data	35	2025-12-09 16:43:08.801089+07	NpgsqlDbType.Json	0
193eb8cb-296f-4e57-8bd4-5ed4dbbf6bc9	acc_srv_account_id	0bee804a-0ca9-4889-87ba-0a692ee97988	account_id	27	2025-12-12 15:25:23.688834+07	NpgsqlDbType.Uuid	0
e58511d6-69f7-4b85-9924-2a57977ac5e1	host_code	3c00c6b9-13be-4600-a7b3-27cefe00f54a	host_code	22	2025-12-13 15:16:59.193417+07	NpgsqlDbType.Varchar	0
117cf40e-a2a6-4520-a856-3623692299b8	version_id	3c00c6b9-13be-4600-a7b3-27cefe00f54a	version_id	27	2025-12-13 15:16:59.301589+07	NpgsqlDbType.Uuid	0
bd3a0eb9-3f74-499a-ad92-9d0329ebe83b	controller_codes	a029a5aa-1e2b-4f7a-a828-563ec3ef898f	controller_codes	-2147483626	2025-12-13 16:55:30.974283+07	\N	0
7f2d823a-c7c1-489e-a8cf-34b388b48f68	account_id	a029a5aa-1e2b-4f7a-a828-563ec3ef898f	account_id	27	2025-12-13 16:55:30.930463+07	NpgsqlDbType.Uuid	0
8ed93779-4e68-4365-b6ff-ebb5afa386d3	codes	808ca6a9-d84e-4401-a5dd-5cd0fe132342	codes	-2147483626	2025-12-14 13:27:48.966392+07	\N	0
f473880a-3c4d-442a-b545-8e36f25d1532	account_id	51acad4e-9f4a-4d5f-930d-a154085775ee	account_id	27	2025-12-14 15:51:03.886838+07	NpgsqlDbType.Uuid	0
61e4f463-3c32-452f-95f4-3a84fe91570d	locale_ids	4f310ed1-0907-4e49-b1ef-ad4bf7ab0cfd	locale_ids	-2147483621	2025-12-16 23:18:27.866327+07	\N	0
b973cec8-cf8c-4985-a9a3-ec9ec178eea6	country_ids	56d1fe5c-72eb-41fc-88b1-94b2963a6df9	country_ids	-2147483621	2025-12-16 15:53:40.033607+07	\N	0
ebcc02c9-8479-4829-83f5-fb72d5466427	lang_code	39f2d3e4-a6a6-4db8-aed5-ce35a3892550	lang_code	22	2025-12-16 23:28:57.111059+07	NpgsqlDbType.Varchar	0
6d941d38-bb0a-4e55-93db-acf8b7696559	lang_code	128f26a2-6324-4749-82f3-39fd1a022009	lang_code	22	2025-12-16 23:31:08.09221+07	NpgsqlDbType.Varchar	0
b2640eb8-726f-4490-b4aa-ec551154915a	resource_key	536be301-e81d-45ba-9573-e203f76f1aab	resource_key	22	2025-12-16 23:34:55.520605+07	NpgsqlDbType.Varchar	0
3f9ea941-ef72-4070-a413-ea99e55c5751	page_size	39f2d3e4-a6a6-4db8-aed5-ce35a3892550	page_size	9	2025-12-16 23:57:31.700454+07	NpgsqlDbType.Integer	0
7a67894a-8d5b-44a4-94c0-1bd846854982	page_index	39f2d3e4-a6a6-4db8-aed5-ce35a3892550	page_index	9	2025-12-16 23:57:31.86128+07	NpgsqlDbType.Integer	0
0003bc2e-dbd0-4872-b576-25745dd686b7	resource_keys	128f26a2-6324-4749-82f3-39fd1a022009	resource_keys	-2147483626	2025-12-16 23:31:08.228191+07	\N	0
bd506fde-cc6e-45bd-95e6-e1d7e57fd077	Parameter @keyword	70e08bdf-dddd-4d9b-b3bc-791ccf1b1af9	keyword	19	2026-02-09 04:53:49.335077+07	NpgsqlDbType.Text	0
b0402f89-b1e1-4764-8c6f-1d87ad676565	Parameter @page_size	70e08bdf-dddd-4d9b-b3bc-791ccf1b1af9	page_size	9	2026-02-09 04:53:49.335199+07	NpgsqlDbType.Integer	1
fe960824-a031-45e3-bcd0-503ee5d3c900	Parameter @page_index	70e08bdf-dddd-4d9b-b3bc-791ccf1b1af9	page_index	9	2026-02-09 04:53:49.335314+07	NpgsqlDbType.Integer	2
d791332d-ee95-4a66-b83d-76a28b9f1ceb	page_size	a5321464-9a97-4851-86a4-d9d3b44e34ea	page_size	9	2025-12-17 09:22:44.388041+07	NpgsqlDbType.Integer	0
0d109b6d-4d10-4c49-98d1-1850a2a628ba	page_index	a5321464-9a97-4851-86a4-d9d3b44e34ea	page_index	9	2025-12-17 09:22:44.443357+07	NpgsqlDbType.Integer	0
9c90c0ed-b3ee-43c5-9f91-5041f9e3cf22	Parameter @dealer_ids	0b262339-226b-4246-bd61-9d35de6b518e	dealer_ids	-2147483621	2025-12-27 16:40:29.39675+07	\N	0
cec16c90-34d8-4bc6-af78-7c9d017d8b7a	lang_codes	efe8ec4e-4c7d-4e5f-8d55-081e0f8023a4	lang_codes	-2147483626	2025-12-18 13:41:31.876951+07	\N	0
9accf261-87bd-4707-9369-1d7a4804efe3	lang_code	0b33adc0-b348-492b-98d9-67a204e0b0d5	lang_code	22	2025-12-18 14:56:47.021316+07	NpgsqlDbType.Varchar	0
38e082f2-5d97-40fc-8626-58d5ecf0d733	keyword	0b33adc0-b348-492b-98d9-67a204e0b0d5	keyword	22	2025-12-18 14:56:47.054049+07	NpgsqlDbType.Varchar	0
47e1c52d-fc47-48cb-8ed7-c38f6ec69bd8	page_size	0b33adc0-b348-492b-98d9-67a204e0b0d5	page_size	9	2025-12-18 14:56:47.083806+07	NpgsqlDbType.Integer	0
656f8054-9793-4963-844c-9f421fa88ef8	page_index	0b33adc0-b348-492b-98d9-67a204e0b0d5	page_index	9	2025-12-18 14:56:47.117044+07	NpgsqlDbType.Integer	0
7d47cc69-6ad2-445c-ad95-916231215a7c	ids	189d19a8-794b-412a-8b3a-3f30bc8d0ffa	ids	-2147483621	2025-12-19 10:57:55.210691+07	\N	0
8aa4c51a-3204-4916-b3cf-1539ec84643c	group_ids	9b8c8408-cf9e-461a-ab99-794a47e3960f	group_ids	-2147483621	2025-12-19 12:01:57.822909+07	\N	0
7e80892d-bf55-48e5-9711-bad5c8ee390e	page_size	9b8c8408-cf9e-461a-ab99-794a47e3960f	page_size	9	2025-12-19 14:28:39.33066+07	NpgsqlDbType.Integer	0
0fbc78f0-1e99-456a-a9ff-994965d2fa48	page_index	9b8c8408-cf9e-461a-ab99-794a47e3960f	page_index	9	2025-12-19 14:28:39.379711+07	NpgsqlDbType.Integer	0
661fd801-069e-4911-9aab-74728414feb8	province_name	ad782489-620c-4198-8a49-f4eb5a780f7f	province_name	22	2025-12-22 12:05:12.367386+07	NpgsqlDbType.Varchar	0
5bc9aaa1-f172-4bdf-8286-e482fcd2a2c3	province_code	ad782489-620c-4198-8a49-f4eb5a780f7f	province_code	22	2025-12-22 12:06:18.129263+07	NpgsqlDbType.Varchar	0
5c946ba0-6a5a-4975-9f59-7af1f93e50d2	country_id	ad782489-620c-4198-8a49-f4eb5a780f7f	country_id	27	2025-12-22 12:06:18.129263+07	NpgsqlDbType.Uuid	0
cdf1fd38-56e8-405d-8efc-9799bf541d39	page_index	ad782489-620c-4198-8a49-f4eb5a780f7f	page_index	9	2025-12-22 12:08:43.559743+07	NpgsqlDbType.Integer	0
f5045ec7-a543-40ff-85ec-31011528b0f4	page_size	ad782489-620c-4198-8a49-f4eb5a780f7f	page_size	9	2025-12-22 12:08:43.559743+07	NpgsqlDbType.Integer	0
f9bdd453-7f72-407f-aed5-4e3d987af2a7	dealer_ids	cb7c46a3-c40b-437e-a01b-73cbbb5174d4	dealer_ids	-2147483621	2025-12-22 15:51:04.083765+07	\N	0
65442c0e-612b-4186-8bdd-832ff3b64fa3	province_id	8712e369-cfab-4567-8c41-229be494aa15	province_id	27	2025-12-22 15:57:50.682245+07	NpgsqlDbType.Uuid	0
faa06f3e-8227-4ffc-98ce-09921a6429cf	resource_module	0b33adc0-b348-492b-98d9-67a204e0b0d5	resource_module	9	2025-12-29 12:56:21.044889+07	NpgsqlDbType.Integer	0
22814735-87aa-4349-8e79-65c9d1e53d29	account_id	c0acc2a2-336f-4690-833a-cceb7ce9c2f0	account_id	27	2026-02-10 14:42:51.854438+07	NpgsqlDbType.Uuid	0
ebc485b3-24a4-4056-a4bc-2cbfd33172ea	Parameter @level_ids	8cf74290-1174-4c4c-a177-591a8a2aa5c4	level_ids	-2147483621	2025-12-30 06:58:34.401535+07	\N	0
01a3cc7c-1274-4749-858d-b4332ff8c91b	Parameter @keyword	df987fa8-0ce2-4c60-86fa-46f34d69953f	keyword	19	2025-12-30 07:20:49.470687+07	NpgsqlDbType.Text	0
8e1693ef-47f0-4d5f-9300-0c275eef1320	is_enable_translate	a5321464-9a97-4851-86a4-d9d3b44e34ea	is_enable_translate	2	2025-12-17 09:21:40.331061+07	NpgsqlDbType.Boolean	0
c7a3e7f5-8662-43a8-8544-e7e088799b5c	dealer_level_status	2544ae14-69c1-4d40-a160-590ab777ca9b	dealer_level_status	9	2025-12-31 11:47:28.180593+07	NpgsqlDbType.Integer	0
17c225e4-4791-4ae6-8079-8a95543784d0	Parameter @account_id	5f7834e4-5578-41ce-93bb-bbd531dff235	account_id	27	2026-01-01 15:38:27.419239+07	NpgsqlDbType.Uuid	0
01a0fd20-1909-47a3-937a-1e770ea8b570	Parameter @dealer_id	5f7834e4-5578-41ce-93bb-bbd531dff235	dealer_id	27	2026-01-01 15:38:27.464861+07	NpgsqlDbType.Uuid	1
60ca86b3-2d3e-41a7-a58f-691602a7af79	Parameter @page_size	5f7834e4-5578-41ce-93bb-bbd531dff235	page_size	9	2026-01-01 15:38:27.464992+07	NpgsqlDbType.Integer	2
c42bc80d-2f52-42aa-a18d-af7a5b8ae790	Parameter @product_ids	da1d4e7f-0090-409d-ae02-10c4a8d95343	product_ids	-2147483621	2026-01-02 10:56:15.772716+07	\N	0
101bec5b-b5a2-406e-a50b-4aa4a691ca0f	Parameter @keyword	042cb5a7-8c2a-4241-92ce-638ecf30fca8	keyword	19	2026-01-05 02:53:22.011977+07	NpgsqlDbType.Text	2
2b008c7e-279d-44c0-babd-9c84968982fe	Parameter @page_size	042cb5a7-8c2a-4241-92ce-638ecf30fca8	page_size	9	2026-01-05 02:53:22.012133+07	NpgsqlDbType.Integer	3
a5b5b950-d64f-4f27-b05a-8a6749ff99cf	Parameter @page_index	042cb5a7-8c2a-4241-92ce-638ecf30fca8	page_index	9	2026-01-05 02:53:22.012297+07	NpgsqlDbType.Integer	4
acbabf9e-38c3-4416-a543-b79adf54a33c	Parameter @attribute_ids	03a817f2-bc6c-4e57-a855-ecf65516c2e4	attribute_ids	-2147483621	2026-01-05 03:11:15.703302+07	\N	0
55dcfa80-31fa-4dfa-b1e2-bcb5b1f85e09	status	a5321464-9a97-4851-86a4-d9d3b44e34ea	status	9	2025-12-17 09:22:44.315699+07	NpgsqlDbType.Integer	0
3b53838f-8ade-4728-abe8-8e7043b2e61e	Parameter @category_ids	042cb5a7-8c2a-4241-92ce-638ecf30fca8	category_ids	-2147483621	2026-01-05 02:53:22.011764+07	\N	1
ec4a0d9d-77cc-50be-9c7f-30d703f25f69	Parameter @warehouse_ids	f022068c-5fe0-4315-873d-e247e72010b8	warehouse_ids	-2147483621	2026-01-06 03:07:17.349127+07	\N	\N
f83ce45e-9f32-494b-a7d3-401da8401f31	Parameter @serial_number	f5651437-15b7-419d-b3d5-5ea1b3f29126	serial_number	22	2026-01-07 05:04:44.677633+07	NpgsqlDbType.Varchar	1
cbadc90a-ac69-4a1b-ad06-84c62eee9bcc	Parameter @title_key	fb626783-7e46-48fa-971d-c5e70d86ca0a	title_key	22	2026-01-06 10:46:06.774836+07	NpgsqlDbType.Varchar	0
abbd33ad-a13c-48ad-8a51-dd039dcdb91a	Parameter @page_index	5f7834e4-5578-41ce-93bb-bbd531dff235	page_index	9	2026-01-01 15:38:27.465106+07	NpgsqlDbType.Integer	3
79df17de-4aa9-470c-9834-65d2616d2b05	keyword	a5321464-9a97-4851-86a4-d9d3b44e34ea	keyword	22	2025-12-17 09:22:44.345728+07	NpgsqlDbType.Varchar	0
c232efd3-cd77-42d1-b035-e351719852f0	Parameter @account_ids	cc3ab878-8a7e-4994-84b3-c0eaad0e7ea3	account_ids	-2147483621	2025-12-26 07:48:42.966472+07	\N	0
12f2488f-13ca-42ca-b825-8325b424867a	Parameter @product_ids	8593f082-a2fb-4b68-97e4-5a194d81c1d3	product_ids	-2147483621	2026-01-07 06:02:06.520148+07	\N	0
47bf6270-6ad9-41b4-a138-a2e1dc2042e1	Parameter @serial_numbers	8593f082-a2fb-4b68-97e4-5a194d81c1d3	serial_numbers	-2147483626	2026-01-07 06:02:06.520151+07	\N	1
22ab06d4-4592-46ba-9a6d-48a30d487def	Parameter @ids	03cb9ef8-ce20-41ef-9226-9e52af3bb0cd	ids	-2147483621	2026-02-09 04:56:19.50478+07	\N	0
e57cf1c7-f64d-4e30-84e5-c176f6bd735d	Parameter @unit_ids	5590e7ef-755c-461b-b0f9-c7cbe45a6e14	unit_ids	-2147483621	2026-01-07 07:57:23.040775+07	\N	0
5203aadc-5599-43ed-9feb-d5f2c1e67335	Parameter @district_id	1a0d49a2-9961-4339-8e06-c1eb04f95775	district_id	27	2026-01-07 15:44:10.481145+07	NpgsqlDbType.Uuid	0
4b4f3db8-0f5d-4672-8600-5a1bf5369c24	Parameter @province_id	1a0d49a2-9961-4339-8e06-c1eb04f95775	province_id	27	2026-01-07 15:44:10.551278+07	NpgsqlDbType.Uuid	1
d6a21b71-bb04-4030-a9e5-322b60541585	Parameter @ward_code	1a0d49a2-9961-4339-8e06-c1eb04f95775	ward_code	22	2026-01-07 15:44:10.63107+07	NpgsqlDbType.Varchar	3
d66786f8-1f91-4c5b-8a7f-888523145812	Parameter @page_size	1a0d49a2-9961-4339-8e06-c1eb04f95775	page_size	9	2026-01-07 15:44:10.633883+07	NpgsqlDbType.Integer	4
79cb884f-37c6-48d6-abb9-d70745651a82	Parameter @page_index	1a0d49a2-9961-4339-8e06-c1eb04f95775	page_index	9	2026-01-07 15:44:10.635692+07	NpgsqlDbType.Integer	5
b5d83216-9822-4fbc-8c89-bb6454d01576	Parameter @ward_id	441dcd25-b673-42db-b06d-dc435b7b0de1	ward_id	27	2026-01-07 15:49:15.270596+07	NpgsqlDbType.Uuid	0
aa860f4d-a4fe-4577-94f3-acd05cf672c0	Parameter @product_ids	c5903fd2-e12d-49b0-97e7-0e942498b03a	product_ids	-2147483621	2026-01-08 02:12:36.188249+07	\N	0
e5e4d476-a00c-4751-8ad8-346c72fef53a	Parameter @serial_numbers	c5903fd2-e12d-49b0-97e7-0e942498b03a	serial_numbers	-2147483626	2026-01-08 02:12:36.188275+07	\N	1
23c64985-2af1-4a07-89fa-1069f01934c7	Parameter @warehouse_id	047a580f-2df8-4445-9198-5155af94bf2c	warehouse_id	27	2026-01-08 08:31:55.280148+07	NpgsqlDbType.Uuid	0
bc27e0ca-69f2-48dd-ba40-ddd3d8b366f7	Parameter @lang_code	7ab10274-6bc1-4eab-bc15-e3031e46c095	lang_code	22	2026-01-08 09:11:52.818349+07	NpgsqlDbType.Varchar	0
77af5257-0f76-48ea-92f1-3ef95ca9bcd7	Parameter @resource_module	7ab10274-6bc1-4eab-bc15-e3031e46c095	resource_module	9	2026-01-08 09:11:52.855364+07	NpgsqlDbType.Integer	1
78cea8b3-7d51-4c02-a4f1-4ab3aeb9c721	Parameter @dealer_id	5cd6c3de-7f4d-4ce3-88a5-b61fa0d99df6	dealer_id	27	2026-01-09 03:20:02.542118+07	NpgsqlDbType.Uuid	0
2c53e94a-6ffa-4c21-a012-d05e04ea7fe0	Parameter @dealer_level_id	5cd6c3de-7f4d-4ce3-88a5-b61fa0d99df6	dealer_level_id	27	2026-01-09 03:20:02.57478+07	NpgsqlDbType.Uuid	1
7a664abc-6e68-4b3f-912c-dbe15e947ed4	Parameter @apply_type	5cd6c3de-7f4d-4ce3-88a5-b61fa0d99df6	apply_type	9	2026-01-09 03:20:02.648385+07	NpgsqlDbType.Integer	3
353996a7-22c4-42d7-be48-fd2cc2cffe9c	Parameter @end_date	5cd6c3de-7f4d-4ce3-88a5-b61fa0d99df6	end_date	26	2026-01-09 03:20:02.650407+07	NpgsqlDbType.TimestampTz	7
fe4fa232-0818-492c-bd5b-2e19b447a17b	Parameter @end_price	5cd6c3de-7f4d-4ce3-88a5-b61fa0d99df6	end_price	13	2026-01-09 03:20:02.649445+07	NpgsqlDbType.Numeric	5
d5cfb1a6-45d0-4a40-8b76-dacde73ef9f9	Parameter @start_price	5cd6c3de-7f4d-4ce3-88a5-b61fa0d99df6	start_price	13	2026-01-09 03:20:02.64896+07	NpgsqlDbType.Numeric	4
d240fa32-9a90-4536-837f-b012de1abf45	Parameter @start_date	5cd6c3de-7f4d-4ce3-88a5-b61fa0d99df6	start_date	26	2026-01-09 03:20:02.649904+07	NpgsqlDbType.TimestampTz	6
24826763-89a4-4c76-affa-5b5a5826ae91	Parameter @ids	49564f75-e92b-44c7-a9b7-09f38023700c	ids	-2147483621	2026-01-09 03:38:59.961225+07	\N	0
999e6c13-9d3d-4535-849d-f2307c40005d	Parameter @category_ids	b35a6088-3926-4306-9009-8615d7efdb32	category_ids	-2147483621	2026-01-09 16:55:44.548626+07	\N	0
91991682-7089-4532-b5b9-959fb15d22cb	Parameter @parent_category_id	a32589fc-1f0a-4b7d-bfbc-e872fa3babce	parent_category_id	19	2026-01-09 17:42:04.38126+07	NpgsqlDbType.Text	0
b630c827-df98-4ef1-9e56-d9eeb0c9761c	Parameter @keyword	a32589fc-1f0a-4b7d-bfbc-e872fa3babce	keyword	19	2026-01-09 17:42:04.382068+07	NpgsqlDbType.Text	1
c1446ca7-e246-4977-9ff3-fa4d230b3011	Parameter @page_index	a32589fc-1f0a-4b7d-bfbc-e872fa3babce	page_index	9	2026-01-09 17:42:04.384449+07	NpgsqlDbType.Integer	3
8c2b6482-06a9-4c61-9ef7-594aa29970cc	Parameter @page_size	a32589fc-1f0a-4b7d-bfbc-e872fa3babce	page_size	9	2026-01-09 17:42:04.383411+07	NpgsqlDbType.Integer	2
e400105a-7687-4271-b02f-6930388de93d	Parameter @is_only_show_base_category	a32589fc-1f0a-4b7d-bfbc-e872fa3babce	is_only_show_base_category	2	2026-01-10 11:00:52.466914+07	NpgsqlDbType.Boolean	4
2660e8bc-2dc5-4101-9ee6-94a78436d43e	is_only_show_variant_category	a32589fc-1f0a-4b7d-bfbc-e872fa3babce	is_only_show_variant_category	2	2026-01-10 11:24:14.010759+07	NpgsqlDbType.Boolean	5
f9f0041f-15fd-4d3a-ae4a-32df0e3e7426	Parameter @product_ids	f300715d-a2e2-4714-8489-6e0b00f01817	product_ids	-2147483621	2026-01-10 12:44:00.768512+07	\N	0
506a5d46-2c23-479a-b557-b9d3561e938c	Parameter @apply_type	f300715d-a2e2-4714-8489-6e0b00f01817	apply_type	9	2026-01-10 12:44:00.770244+07	NpgsqlDbType.Integer	3
55e0fd6a-e1eb-433c-a306-5d1f61304c12	Parameter @dealer_level_id	f300715d-a2e2-4714-8489-6e0b00f01817	dealer_level_id	27	2026-01-10 12:44:00.769666+07	NpgsqlDbType.Uuid	2
441cdcba-e81a-4167-aea1-301674cc1e38	Parameter @dealer_id	f300715d-a2e2-4714-8489-6e0b00f01817	dealer_id	27	2026-01-10 12:44:00.769097+07	NpgsqlDbType.Uuid	1
43e2d033-d7bc-4094-8154-f07a3174b0b3	Parameter @is_abstract	042cb5a7-8c2a-4241-92ce-638ecf30fca8	is_abstract	2	2026-01-05 02:53:22.011+07	NpgsqlDbType.Boolean	0
958d628b-9373-4f49-8d85-af5f5af14061	category_ids	f5f12557-da48-4c9c-97b0-051ffb966e0f	category_ids	-2147483621	2026-01-10 23:55:51.743058+07	\N	0
6bc70248-1ce7-4c9e-805e-d276a2d0492f	is_find_children	f5f12557-da48-4c9c-97b0-051ffb966e0f	is_find_children	2	2026-01-10 23:55:51.776905+07	NpgsqlDbType.Boolean	1
be0ba17d-0b30-43d8-9695-edb91fd1c1c4	is_find_parent	f5f12557-da48-4c9c-97b0-051ffb966e0f	is_find_parent	2	2026-01-10 23:55:51.808938+07	NpgsqlDbType.Boolean	2
e308602f-5610-41c4-91c4-d2305aacec55	Parameter @keyword	642f2c28-7f16-4f7b-acb1-1df43a66b036	keyword	19	2026-01-12 14:53:27.932172+07	NpgsqlDbType.Text	0
a72dd5a2-c1a8-492d-b22c-db8aac0d6efd	Parameter @fullname	642f2c28-7f16-4f7b-acb1-1df43a66b036	fullname	19	2026-01-12 14:53:27.932699+07	NpgsqlDbType.Text	1
32272d86-62a7-43ba-89b0-02fab1f4a44a	Parameter @email	642f2c28-7f16-4f7b-acb1-1df43a66b036	email	19	2026-01-12 14:53:27.933196+07	NpgsqlDbType.Text	2
4fe09095-e86c-4d34-a9eb-047e0dc85281	Parameter @phone_number	642f2c28-7f16-4f7b-acb1-1df43a66b036	phone_number	19	2026-01-12 14:53:27.933708+07	NpgsqlDbType.Text	3
9d3d2f3a-24c9-44e1-bf05-e2655b707d5a	Parameter @country_ids	642f2c28-7f16-4f7b-acb1-1df43a66b036	country_ids	-2147483621	2026-01-12 14:53:27.933769+07	\N	5
02d0c319-07d5-42a3-9e3c-ccbffb3b82dc	Parameter @province_ids	642f2c28-7f16-4f7b-acb1-1df43a66b036	province_ids	-2147483621	2026-01-12 14:53:27.933779+07	\N	6
39ed4666-fd65-404c-88da-9097dbdf7421	Parameter @district_ids	642f2c28-7f16-4f7b-acb1-1df43a66b036	district_ids	-2147483621	2026-01-12 14:53:27.933798+07	\N	7
3f982871-b793-46b1-a277-cc40af9312bc	Parameter @ward_ids	642f2c28-7f16-4f7b-acb1-1df43a66b036	ward_ids	-2147483621	2026-01-12 14:53:27.933845+07	\N	8
3723aee0-ed56-45ad-b87b-9194333adf23	Parameter @address_line	642f2c28-7f16-4f7b-acb1-1df43a66b036	address_line	19	2026-01-12 14:53:27.934439+07	NpgsqlDbType.Text	9
6f7c64f0-5d03-48fd-9a23-a1e1535e23ce	Parameter @current_lang_code	642f2c28-7f16-4f7b-acb1-1df43a66b036	current_lang_code	22	2026-01-12 14:53:27.934924+07	NpgsqlDbType.Varchar	10
231a94b0-30aa-4c3c-9371-ada9ba203ec2	Parameter @gender	642f2c28-7f16-4f7b-acb1-1df43a66b036	gender	9	2026-01-12 14:53:27.93374+07	NpgsqlDbType.Integer	4
9dcc9785-7e52-4f39-b0f8-402b378cb325	Parameter @ward_name	1a0d49a2-9961-4339-8e06-c1eb04f95775	ward_name	22	2026-01-07 15:44:10.555853+07	NpgsqlDbType.Varchar	2
343c08a1-46fa-45d5-839d-f8d6332a2772	Parameter @product_ids	5cd6c3de-7f4d-4ce3-88a5-b61fa0d99df6	product_ids	-2147483626	2026-01-09 03:20:02.611821+07	\N	2
4331c3b5-b6d2-4f4e-9c05-af60b761a17b	Parameter @page_size	75821f79-eb21-4dfa-afb1-0472ce72ef75	page_size	9	2026-02-24 08:48:34.51116+07	NpgsqlDbType.Integer	2
da36f2a4-aa1f-4035-a800-031505489bec	Parameter @page_index	75821f79-eb21-4dfa-afb1-0472ce72ef75	page_index	13	2026-02-24 08:48:34.511667+07	NpgsqlDbType.Numeric	3
c6c3d946-3c3a-4a9d-9a05-b6311ee723c2	Parameter @permissions	75821f79-eb21-4dfa-afb1-0472ce72ef75	permissions	-2147483639	2026-02-24 08:48:34.510257+07	\N	0
ae70dd28-70d6-44f1-bf0b-2c1f4c67d957	Parameter @account_id	75821f79-eb21-4dfa-afb1-0472ce72ef75	account_id	27	2026-02-24 08:48:34.510785+07	NpgsqlDbType.Unknown	1
8f397f47-cdf4-43cf-8d31-a887845ec527	Parameter @account_id	e2689eb4-88ce-46f2-8ea8-792eae1fb24d	account_id	27	2026-02-27 05:56:29.755727+07	NpgsqlDbType.Uuid	2
ab6e76d4-c3e5-4350-95d5-d20ab67098d5	Parameter @page_index	e2689eb4-88ce-46f2-8ea8-792eae1fb24d	page_index	9	2026-02-27 05:56:29.755731+07	NpgsqlDbType.Integer	4
37dd1cdc-b4de-4a58-8edb-fe4cba5c1450	Parameter @page_size	e2689eb4-88ce-46f2-8ea8-792eae1fb24d	page_size	9	2026-02-27 05:56:29.755729+07	NpgsqlDbType.Integer	3
933ec069-7900-4b95-b37e-c5e3c4445917	Parameter @permissions	e2689eb4-88ce-46f2-8ea8-792eae1fb24d	permissions	-2147483639	2026-02-27 05:56:29.755725+07	\N	1
8bfbdd01-aa6b-4633-b527-a6c5505bd6cb	Parameter @stage	e2689eb4-88ce-46f2-8ea8-792eae1fb24d	stage	9	2026-02-27 05:56:29.755722+07	NpgsqlDbType.Integer	0
ba8e3e21-be6c-4965-85ad-a6c5fb4ced88	Parameter @page_size	642f2c28-7f16-4f7b-acb1-1df43a66b036	page_size	9	2026-01-12 14:53:27.935886+07	NpgsqlDbType.Integer	13
97e85439-ee7f-4828-9ae9-feb1d57073c6	Parameter @page_index	642f2c28-7f16-4f7b-acb1-1df43a66b036	page_index	9	2026-01-12 14:53:27.936338+07	NpgsqlDbType.Integer	14
80f88874-b6a0-4201-8e8a-4131560f94b3	province_ids	6248297f-9310-421a-adb4-6960d1ad6c03	province_ids	-2147483621	2026-01-12 22:51:14.341514+07	\N	0
a9ad8e04-ff50-4062-8989-167f17242f55	ward_ids	c73bf55f-7413-4d4b-aa0a-87d0d4e5ba26	ward_ids	-2147483621	2026-01-12 23:04:08.666245+07	\N	0
76908e61-9c5e-410e-be01-fbd462eb8973	Parameter @page_size	43dae05f-0f31-4862-8752-e9771b7d19a0	page_size	9	2026-02-09 14:28:50.195279+07	NpgsqlDbType.Integer	0
d2c03b46-f4a2-48fb-b530-b63b3696f6b6	Parameter @ids	1c5cb030-e3c7-45bc-a3be-2dcc8207cc1d	ids	-2147483621	2026-01-13 04:40:06.144774+07	\N	0
2de4da16-38c1-44a7-88c9-86fa36940491	Parameter @batch_ids	445580ba-2b74-4cbe-9036-0cc26297d7dc	batch_ids	-2147483621	2026-01-13 06:43:07.689799+07	\N	0
63e2bc88-787d-48e9-abd8-482a98b99126	Parameter @product_ids	5b9b8b25-291f-4e63-b41c-741358853220	product_ids	-2147483621	2026-01-13 16:00:54.090257+07	\N	0
665040a6-2a63-4d87-b8a9-3c620b774811	Parameter @warehouse_id	5b9b8b25-291f-4e63-b41c-741358853220	warehouse_id	27	2026-01-13 16:00:54.132183+07	NpgsqlDbType.Uuid	1
18c31b1c-e985-4b9c-8af5-b44deb86c789	Parameter @province_rcd	ad782489-620c-4198-8a49-f4eb5a780f7f	province_rcd	22	2026-01-14 11:08:30.124611+07	NpgsqlDbType.Varchar	0
b5dc736b-f655-4bdf-a31b-fb3f77eaee82	Parameter @batch_id	56286a71-7293-4791-b754-e29ee1caf10d	batch_id	27	2026-01-14 05:10:22.613747+07	NpgsqlDbType.Uuid	0
bbd08674-40e6-4ce1-aff5-c2be5e40d9f8	Parameter @product_ids	56286a71-7293-4791-b754-e29ee1caf10d	product_ids	-2147483621	2026-01-14 05:10:22.613778+07	\N	1
a34e8577-b45f-4cec-af64-2c287fc7bad1	keyword	bddea19d-6782-45f5-9ed5-2b2bcd81f5de	keyword	22	2026-01-14 12:59:36.732267+07	NpgsqlDbType.Varchar	0
2a5a7ec3-b89e-470d-a9fd-e9c843912bff	page_size	bddea19d-6782-45f5-9ed5-2b2bcd81f5de	page_size	9	2026-01-14 12:59:36.878504+07	NpgsqlDbType.Integer	0
38e05119-0ec6-4772-8458-5fe21add867f	page_index	bddea19d-6782-45f5-9ed5-2b2bcd81f5de	page_index	9	2026-01-14 12:59:36.993492+07	NpgsqlDbType.Integer	0
e77c6354-c914-4eb7-b545-aceb8bc80dca	Parameter @group_id	c3a85536-6790-498a-8fb7-a0d8542649df	group_id	27	2026-01-14 12:41:40.340895+07	NpgsqlDbType.Uuid	0
55a88d06-90c8-43b9-8194-cbac8cd5e561	Parameter @keyword	c3a85536-6790-498a-8fb7-a0d8542649df	keyword	19	2026-01-14 12:41:40.341775+07	NpgsqlDbType.Text	1
61fab2ed-5c89-47a6-92f0-392fce5145b0	Parameter @permissions	c3a85536-6790-498a-8fb7-a0d8542649df	permissions	-2147483639	2026-01-14 12:41:40.34186+07	\N	2
2b2f4e1a-c843-4c36-982f-ea089288dd60	Parameter @page_size	c3a85536-6790-498a-8fb7-a0d8542649df	page_size	9	2026-01-14 12:41:40.342505+07	NpgsqlDbType.Integer	3
6054c5ea-e87d-4a63-923b-3ade341734d5	Parameter @page_index	c3a85536-6790-498a-8fb7-a0d8542649df	page_index	9	2026-01-14 12:41:40.343138+07	NpgsqlDbType.Integer	4
7f93a1f5-4ea0-4738-a6b6-60958f0c741c	Parameter @group_id	02a96d95-4315-40ba-9b00-fe5a7b874603	group_id	27	2026-01-14 13:17:13.208886+07	NpgsqlDbType.Uuid	0
3ba4b914-7fba-4829-8e4e-d938233a7b5f	Parameter @keyword	02a96d95-4315-40ba-9b00-fe5a7b874603	keyword	19	2026-01-14 13:17:13.210098+07	NpgsqlDbType.Text	1
2d7d4a84-0064-4990-8e2d-4d10607c9443	Parameter @page_size	02a96d95-4315-40ba-9b00-fe5a7b874603	page_size	9	2026-01-14 13:17:13.211146+07	NpgsqlDbType.Integer	3
e9be354f-36e4-4fff-9e85-5cb910f62cb7	Parameter @page_index	02a96d95-4315-40ba-9b00-fe5a7b874603	page_index	9	2026-01-14 13:17:13.212083+07	NpgsqlDbType.Integer	4
68b81272-5215-4c0b-9cc8-d13dd9bf889a	Parameter @keyword	ed9825e9-2178-46cc-adc1-251c6e9db188	keyword	19	2026-01-14 13:45:43.576794+07	NpgsqlDbType.Text	0
cc2ad9a2-436f-4012-99ab-215eb2ff885f	Parameter @lead_ids	ed9825e9-2178-46cc-adc1-251c6e9db188	lead_ids	-2147483621	2026-01-14 13:45:43.57686+07	\N	1
fa87ac17-f4c9-477c-81a0-f42bbce77b06	Parameter @group_types	ed9825e9-2178-46cc-adc1-251c6e9db188	group_types	-2147483639	2026-01-14 13:45:43.576903+07	\N	2
7150667e-0519-4cc9-9645-1548a312ec52	Parameter @page_size	ed9825e9-2178-46cc-adc1-251c6e9db188	page_size	9	2026-01-14 13:45:43.577356+07	NpgsqlDbType.Integer	3
7b6a3b8a-7de4-496a-99dc-0162930377cf	Parameter @page_index	ed9825e9-2178-46cc-adc1-251c6e9db188	page_index	9	2026-01-14 13:45:43.577849+07	NpgsqlDbType.Integer	4
e56368e0-985a-419f-b7ce-93cb78dfc38b	Parameter @district_name	095bbb03-7de3-4f7c-8c91-db1e513cac4f	district_name	19	2026-01-15 02:22:42.751805+07	NpgsqlDbType.Text	0
51000e67-f856-43cf-ad59-53feac413484	Parameter @district_code	095bbb03-7de3-4f7c-8c91-db1e513cac4f	district_code	22	2026-01-15 02:22:42.798317+07	NpgsqlDbType.Varchar	1
5310acb1-4bff-43bd-9ba4-f18d821aadb5	Parameter @province_id	095bbb03-7de3-4f7c-8c91-db1e513cac4f	province_id	27	2026-01-15 02:22:42.839743+07	NpgsqlDbType.Uuid	2
e2c58b4a-572b-48b9-bf6e-cfd4b10b1ca7	Parameter @page_size	095bbb03-7de3-4f7c-8c91-db1e513cac4f	page_size	9	2026-01-15 02:22:42.840988+07	NpgsqlDbType.Integer	3
a26623b0-2aca-4d8b-a10e-8ec883366785	Parameter @page_index	095bbb03-7de3-4f7c-8c91-db1e513cac4f	page_index	9	2026-01-15 02:22:42.843731+07	NpgsqlDbType.Integer	4
a6cfce08-d1e8-44f8-abe1-0b75019bd416	Parameter @country_id	1a0d49a2-9961-4339-8e06-c1eb04f95775	country_id	27	2026-01-15 12:23:40.502048+07	NpgsqlDbType.Uuid	6
cc1ba5cf-ec11-4d18-a30d-794233a2ea56	Parameter @country_id	095bbb03-7de3-4f7c-8c91-db1e513cac4f	country_id	27	2026-01-15 12:50:33.657289+07	NpgsqlDbType.Uuid	5
68ed6771-c6a2-4187-bdd3-c8a3229ef343	Parameter @warehouse_id	61dcdcca-24b0-45b9-bab9-6247ec7bf17a	warehouse_id	27	2026-01-15 06:48:15.199039+07	NpgsqlDbType.Uuid	0
90fdb6e3-002b-4c5d-8eb4-f4f7b26efb17	product_ids	bddea19d-6782-45f5-9ed5-2b2bcd81f5de	product_ids	-2147483621	2026-01-14 12:59:36.732+07	\N	0
e7adf49e-744d-4bf4-a06d-a76fdac11cd7	warehouse_ids	bddea19d-6782-45f5-9ed5-2b2bcd81f5de	warehouse_ids	-2147483621	2026-01-14 12:59:36.732+07	\N	0
73b66413-6e08-4e2d-acd7-d2147b99ba09	warehouse_name	bddea19d-6782-45f5-9ed5-2b2bcd81f5de	warehouse_name	22	2026-01-14 12:59:36.732+07	NpgsqlDbType.Varchar	0
d0519596-834d-48db-9eb3-e698633748e1	product_name	bddea19d-6782-45f5-9ed5-2b2bcd81f5de	product_name	22	2026-01-14 12:59:36.732+07	NpgsqlDbType.Varchar	0
155b48a8-a719-438a-83ad-beb504aacd1f	keyword	8a8c36bf-6676-4929-b831-132467c7846a	keyword	22	2026-01-15 15:55:01.483714+07	NpgsqlDbType.Varchar	0
d30f80e1-96d4-4135-80b2-046b5906094e	page_size	8a8c36bf-6676-4929-b831-132467c7846a	page_size	9	2026-01-15 15:55:01.514819+07	NpgsqlDbType.Integer	0
1a8f1d71-0f52-4a64-9a31-e32593c4a821	page_index	8a8c36bf-6676-4929-b831-132467c7846a	page_index	9	2026-01-15 15:55:01.54459+07	NpgsqlDbType.Integer	0
241b850c-c1fe-4e4d-b64a-a223062a8c68	product_ids	8a8c36bf-6676-4929-b831-132467c7846a	product_ids	-2147483621	2026-01-15 15:55:01.570749+07	\N	0
0cde4d0f-f4e0-40ad-b3cd-eed27164de06	warehouse_ids	8a8c36bf-6676-4929-b831-132467c7846a	warehouse_ids	-2147483621	2026-01-15 15:55:01.597587+07	\N	0
b903b22d-b896-4999-ade0-503a8ccca7b6	warehouse_name	8a8c36bf-6676-4929-b831-132467c7846a	warehouse_name	22	2026-01-15 15:55:01.625181+07	NpgsqlDbType.Varchar	0
6af0d783-c381-47d0-87c2-d17d738af961	product_name	8a8c36bf-6676-4929-b831-132467c7846a	product_name	22	2026-01-15 15:55:01.65165+07	NpgsqlDbType.Varchar	0
82ca886b-ab9c-4060-afe6-662af2b5c7a8	Parameter @district_id	271935ff-127c-4d59-99dc-23b3c4799fe9	district_id	27	2026-01-15 10:42:12.022793+07	NpgsqlDbType.Uuid	0
03f53324-a926-4ead-9ba3-4b45c83d401d	Parameter @product_ids	d34fbf5c-aaef-4cf6-adc5-42d2bfce261f	product_ids	-2147483621	2026-01-15 16:06:25.716262+07	\N	0
92b1e7e6-ffdd-422a-a460-2885c7a724c1	category_parent_ids	708e30ca-3024-4886-94ef-4348fe4147ad	category_parent_ids	-2147483621	2026-01-09 16:55:44.548+07	\N	0
f8ead83b-1ced-423f-9196-b0701f493667	page_index	0060dcdf-5421-4f07-8507-03a08d99b691	page_index	9	2026-01-19 15:26:51.414304+07	NpgsqlDbType.Integer	0
04f738ad-74de-4a6c-bcc2-09c9bb809275	page_size	0060dcdf-5421-4f07-8507-03a08d99b691	page_size	9	2026-01-19 15:26:51.414304+07	NpgsqlDbType.Integer	0
a66d2356-d2d4-4bb7-bbf3-8dc42285a8b0	Parameter @page_index	43dae05f-0f31-4862-8752-e9771b7d19a0	page_index	13	2026-02-09 14:28:50.227348+07	NpgsqlDbType.Numeric	0
0b36796b-ca3c-4282-8f69-f21ef51f28b0	Parameter @keyword	43dae05f-0f31-4862-8752-e9771b7d19a0	keyword	22	2026-02-09 14:28:50.256185+07	NpgsqlDbType.Varchar	0
2b8d6c04-9a7b-4f9c-b321-6e5d4c3b2a11	end_date	0060dcdf-5421-4f07-8507-03a08d99b691	end_date	26	2026-01-19 15:28:50.432702+07	NpgsqlDbType.TimestampTz	0
9abc5678-1234-4f90-bcde-ef0123456789	end_amount	0060dcdf-5421-4f07-8507-03a08d99b691	end_amount	13	2026-01-19 15:28:50.618534+07	NpgsqlDbType.Numeric	0
8f2a2d8b-47a4-4c4a-b2f7-3b6d1a3e4d10	search_keyword	0060dcdf-5421-4f07-8507-03a08d99b691	search_keyword	22	2026-01-19 15:28:50.235938+07	NpgsqlDbType.Varchar	0
6f7ccac1-4ec1-4488-beca-626ca9ecc003	Parameter @permissions	642f2c28-7f16-4f7b-acb1-1df43a66b036	permissions	-2147483639	2026-01-12 14:53:27.934988+07	\N	11
c60ee4c7-f2b8-4105-8b0b-d2953ce70b5d	Parameter @account_statuses	642f2c28-7f16-4f7b-acb1-1df43a66b036	account_statuses	-2147483639	2026-01-12 14:53:27.935021+07	\N	12
864c7c0c-0473-4a27-b7ed-572f14772324	data_types	43dae05f-0f31-4862-8752-e9771b7d19a0	data_types	-2147483639	2026-02-09 14:28:50.310231+07	\N	0
fe2a3b58-4489-4205-99aa-5aada1e11c68	Parameter @permissions	02a96d95-4315-40ba-9b00-fe5a7b874603	permissions	-2147483639	2026-01-14 13:17:13.210217+07	\N	2
4e6f2a31-5b7c-4d89-a0e2-123456789abc	start_amount	0060dcdf-5421-4f07-8507-03a08d99b691	start_amount	13	2026-01-19 15:28:50.522385+07	NpgsqlDbType.Numeric	0
c6cbbd41-2c7c-4f58-9c6e-91f1e8e9f902	order_code	0060dcdf-5421-4f07-8507-03a08d99b691	order_code	22	2026-01-19 15:28:50.140725+07	NpgsqlDbType.Varchar	0
1a7c5b93-6d4c-4a6e-9a87-1b2c3d4e5f60	start_date	0060dcdf-5421-4f07-8507-03a08d99b691	start_date	26	2026-01-19 15:28:50.33845+07	NpgsqlDbType.TimestampTz	0
b3e0a94f-2c9a-4c53-8c6c-1a6b2a6f5e01	order_status	0060dcdf-5421-4f07-8507-03a08d99b691	order_status	9	2026-01-19 15:28:50.046575+07	NpgsqlDbType.Integer	0
737f4cd2-7a86-44ee-af49-141c6965e6f0	Parameter @keyword	afe59c0f-bcad-4ae0-a22f-7d7018bba5c7	keyword	19	2026-01-19 08:49:01.130393+07	NpgsqlDbType.Text	0
2011c598-2b06-4a22-abd9-c0f7eac64e10	Parameter @product_name	afe59c0f-bcad-4ae0-a22f-7d7018bba5c7	product_name	19	2026-01-19 08:49:01.131315+07	NpgsqlDbType.Text	1
5abf11f0-d222-4ddb-9c60-d0c873075508	Parameter @product_ids	afe59c0f-bcad-4ae0-a22f-7d7018bba5c7	product_ids	-2147483621	2026-01-19 08:49:01.131465+07	\N	2
4d13d17c-5640-43e4-81df-454e8ce36601	Parameter @product_parent_ids	afe59c0f-bcad-4ae0-a22f-7d7018bba5c7	product_parent_ids	-2147483621	2026-01-19 08:49:01.131545+07	\N	3
cc2417d4-419c-448f-8b48-d68751603f5c	Parameter @is_abstract_product	afe59c0f-bcad-4ae0-a22f-7d7018bba5c7	is_abstract_product	2	2026-01-19 08:49:01.131724+07	NpgsqlDbType.Boolean	6
8d8e37f1-e957-49c2-b07c-6b5f373e81d8	Parameter @page_size	afe59c0f-bcad-4ae0-a22f-7d7018bba5c7	page_size	9	2026-01-19 08:49:01.131793+07	NpgsqlDbType.Integer	7
4248cd7d-2121-4be5-b745-25f9c9803602	Parameter @page_index	afe59c0f-bcad-4ae0-a22f-7d7018bba5c7	page_index	9	2026-01-19 08:49:01.131841+07	NpgsqlDbType.Integer	8
43f596c2-e3a5-4c3f-8361-1fee760fdc03	Parameter @data_types	afe59c0f-bcad-4ae0-a22f-7d7018bba5c7	data_types	-2147483639	2026-01-19 08:49:01.131603+07	\N	4
7028eb21-82e0-4856-a7f6-c4b2dc641ae7	Parameter @select_types	afe59c0f-bcad-4ae0-a22f-7d7018bba5c7	select_types	-2147483639	2026-01-19 08:49:01.131647+07	\N	5
58b5b60b-9b6b-48a9-9d84-e8f87b4ba3c2	page_size	03a817f2-bc6c-4e57-a855-ecf65516c2e4	page_size	9	2026-01-19 16:50:38.306505+07	NpgsqlDbType.Integer	0
0a4e6ce4-4cd8-4e45-8714-5a1120ff0708	page_index	03a817f2-bc6c-4e57-a855-ecf65516c2e4	page_index	9	2026-01-19 16:50:38.340122+07	NpgsqlDbType.Integer	0
05568ce1-8536-4705-a4a7-c2ec534594bb	resource_module	128f26a2-6324-4749-82f3-39fd1a022009	resource_module	9	2026-01-19 17:31:06.678713+07	NpgsqlDbType.Integer	0
7c7211d8-d501-4336-a068-368383aa2bd8	Parameter @account_ids	279d35a4-cb1a-4181-a5b9-420c7a41f5e6	account_ids	-2147483621	2026-01-20 06:36:36.179465+07	\N	0
92091cd1-e316-420f-8539-0afba7dbb286	order_id	0060dcdf-5421-4f07-8507-03a08d99b691	order_id	27	2026-01-20 16:25:46.083935+07	NpgsqlDbType.Uuid	0
cb715fdc-eb77-4c56-a0c0-cc2b78ac799b	Parameter @keyword	7542cb6c-b8f5-49ae-ad66-eb1157848737	keyword	22	2026-02-09 14:31:30.307253+07	NpgsqlDbType.Varchar	0
fc79090d-63d8-4f07-af5b-d2f39d9f2cbf	Parameter @page_index	7542cb6c-b8f5-49ae-ad66-eb1157848737	page_index	13	2026-02-09 14:31:30.341804+07	NpgsqlDbType.Numeric	0
47a349e1-eaa1-4006-bc89-e9d0e09f97ae	Parameter @page_size	7542cb6c-b8f5-49ae-ad66-eb1157848737	page_size	9	2026-02-09 14:31:30.369374+07	NpgsqlDbType.Integer	0
5bc82a76-89c1-4011-8006-32c307955072	logic_types	43dae05f-0f31-4862-8752-e9771b7d19a0	logic_types	-2147483639	2026-02-09 14:28:50.338752+07	\N	0
0482d788-1d52-4dff-bda7-60f5754ea3ff	Parameter @statuses	978f706b-4740-46aa-973c-8126ae15072d	statuses	-2147483639	2026-02-25 07:05:43.057821+07	\N	1
bfbedf6c-6700-412f-a61e-711b5c5eab8a	Parameter @order_ids	31ddadc4-f802-4ad2-85ba-802d4a8eca44	order_ids	-2147483621	2026-01-20 15:41:13.070399+07	\N	0
e6f0a3dd-087f-45d0-a950-4b6475adcdee	Parameter @priority_max	978f706b-4740-46aa-973c-8126ae15072d	priority_max	9	2026-02-25 07:05:43.057814+07	NpgsqlDbType.Integer	0
082922d3-9b37-4017-87e6-7c262383f7e0	ids	39573707-0323-4dfc-a752-6a9c1c715ddc	ids	-2147483621	2026-01-21 09:47:39.995592+07	\N	0
b0375723-efcc-429c-9550-6be19a9c791a	Parameter @ids	07189e6a-e931-4bdf-b4f6-fe102e967b32	ids	-2147483621	2026-01-21 07:09:29.0106+07	\N	0
1ef2e1ed-10e6-4b12-882b-0f8e33e85ddf	Parameter @keyword	ae03ef79-cd26-4738-ad84-e092af77dccb	keyword	19	2026-01-21 08:53:19.980221+07	NpgsqlDbType.Text	0
3fc96bc2-dae9-4a20-9e64-86f408a68905	Parameter @page_index	ae03ef79-cd26-4738-ad84-e092af77dccb	page_index	9	2026-01-21 08:53:19.980612+07	NpgsqlDbType.Integer	2
148b3a66-5519-4272-b01d-858dbdb6ecb9	Parameter @page_size	ae03ef79-cd26-4738-ad84-e092af77dccb	page_size	9	2026-01-21 08:53:19.980851+07	NpgsqlDbType.Integer	3
0b46f32b-25aa-482d-9fae-a16b730ac162	Parameter @status	ae03ef79-cd26-4738-ad84-e092af77dccb	status	9	2026-01-21 08:53:19.980455+07	NpgsqlDbType.Integer	1
54ce5a4d-2f06-41da-b820-35639659d13c	formula_types	43dae05f-0f31-4862-8752-e9771b7d19a0	formula_types	-2147483639	2026-02-09 14:28:50.28352+07	\N	0
eb98b834-d40b-4202-8032-cfc0c42c2074	Parameter @keyword	cc31e23a-8d46-48d2-9bfe-a4ae310779ea	keyword	19	2026-01-21 14:01:00.282184+07	NpgsqlDbType.Text	0
3409c979-0507-42b1-9b9d-13ee0b460f09	Parameter @page_index	cc31e23a-8d46-48d2-9bfe-a4ae310779ea	page_index	9	2026-01-21 14:01:00.282614+07	NpgsqlDbType.Integer	2
ab5f80b6-e0b7-4daa-b903-17221d54b68a	Parameter @page_size	cc31e23a-8d46-48d2-9bfe-a4ae310779ea	page_size	9	2026-01-21 14:01:00.282791+07	NpgsqlDbType.Integer	3
9638935c-849e-4c11-af17-be8b44e0fb4d	Parameter @status	cc31e23a-8d46-48d2-9bfe-a4ae310779ea	status	9	2026-01-21 14:01:00.282417+07	NpgsqlDbType.Integer	1
54fa6a00-d1e0-40b5-b2a1-d60f3c634673	Parameter @ids	18b2db4a-acc1-41a1-bfd3-afd64fbe380c	ids	-2147483621	2026-01-21 14:03:21.904813+07	\N	0
f7cd8f3a-eb6a-480a-8218-96fc623c7022	Parameter @product_ids	ff722538-53ff-4007-8d7e-16686aca7517	product_ids	-2147483621	2026-01-22 02:35:04.602805+07	\N	0
227f305d-e8c3-4fae-80f5-cc1814077377	Parameter @warehouse_ids	ff722538-53ff-4007-8d7e-16686aca7517	warehouse_ids	-2147483621	2026-01-22 02:35:04.602815+07	\N	1
506ff13f-1d4a-4553-b3b8-c57467439ad1	Parameter @country_ids	cc31e23a-8d46-48d2-9bfe-a4ae310779ea	country_ids	-2147483621	2026-01-22 11:12:58.647534+07	\N	4
24dda042-390f-4720-bcab-eaabd16c936c	Parameter @keyword	abe25e7e-a90a-4725-97c8-cc146aa3aad2	keyword	19	2026-01-23 02:20:32.154577+07	NpgsqlDbType.Text	0
b6a16f76-e049-4ca9-bd6d-21fedbc6b9af	Parameter @page_size	abe25e7e-a90a-4725-97c8-cc146aa3aad2	page_size	9	2026-01-23 02:20:32.155312+07	NpgsqlDbType.Integer	5
ab0bbc42-75e7-4ac8-877e-1238fb1e2287	Parameter @district_id	abe25e7e-a90a-4725-97c8-cc146aa3aad2	district_id	27	2026-01-23 02:20:32.15484+07	NpgsqlDbType.Uuid	2
cb45d6ab-b425-41b8-8250-53eaec185ef0	Parameter @ward_id	abe25e7e-a90a-4725-97c8-cc146aa3aad2	ward_id	27	2026-01-23 02:20:32.154714+07	NpgsqlDbType.Uuid	1
ee422472-bb8a-4e6e-a722-a102dc59c922	Parameter @country_id	abe25e7e-a90a-4725-97c8-cc146aa3aad2	country_id	27	2026-01-23 02:20:32.155132+07	NpgsqlDbType.Uuid	4
3aa9758d-9d8b-4df0-a531-e4cb7d653cac	Parameter @page_index	abe25e7e-a90a-4725-97c8-cc146aa3aad2	page_index	9	2026-01-23 02:20:32.155494+07	NpgsqlDbType.Integer	6
0238b506-6024-442b-9b7a-447465820f17	Parameter @province_id	abe25e7e-a90a-4725-97c8-cc146aa3aad2	province_id	27	2026-01-23 02:20:32.154987+07	NpgsqlDbType.Uuid	3
8c616319-153f-47ef-9933-900557965296	group_ids	fa224726-46d0-49b2-9e85-383fcaaf9d33	group_ids	-2147483621	2026-01-23 15:23:32.752827+07	\N	0
9615198e-55bb-4bba-a1aa-044140c90339	Parameter @keyword	e5733381-6e45-4f10-98c0-680a4c50020e	keyword	19	2026-01-27 15:57:52.11343+07	NpgsqlDbType.Text	1
374934c1-97e2-45c7-a21a-fcdcac729940	Parameter @page_size	e5733381-6e45-4f10-98c0-680a4c50020e	page_size	9	2026-01-27 15:57:52.145305+07	NpgsqlDbType.Integer	2
b786ceeb-8f98-45e8-bbef-876084e407f7	Parameter @page_index	e5733381-6e45-4f10-98c0-680a4c50020e	page_index	9	2026-01-27 15:57:52.175627+07	NpgsqlDbType.Integer	3
ffe98c33-c416-40f8-b3f0-d4422d33ce72	Parameter @keyword	1a0d49a2-9961-4339-8e06-c1eb04f95775	keyword	22	2026-01-28 15:29:39.329099+07	NpgsqlDbType.Varchar	7
abd2555f-c393-4973-8b63-701c1abf7dc6	Parameter @keyword	095bbb03-7de3-4f7c-8c91-db1e513cac4f	keyword	22	2026-01-28 16:04:38.499243+07	NpgsqlDbType.Varchar	0
307941cc-71bb-41a6-b2e1-b31713e1e366	Parameter @keyword	ad782489-620c-4198-8a49-f4eb5a780f7f	keyword	22	2026-01-28 16:07:23.759641+07	NpgsqlDbType.Varchar	0
2fe6c25d-d4a8-4963-873d-98530c3f648b	Parameter @product_ids	5e08d675-2475-41c3-bf8e-ef1642c70cd5	product_ids	-2147483621	2026-01-29 08:06:19.277207+07	\N	1
f23ee127-568a-4072-a60d-fbd22a5099ea	Parameter @warehouse_to_ids	5e08d675-2475-41c3-bf8e-ef1642c70cd5	warehouse_to_ids	-2147483621	2026-01-29 08:06:19.277234+07	\N	2
bf0273a0-8534-4962-a160-cc2d72ec6913	Parameter @warehouse_from_ids	5e08d675-2475-41c3-bf8e-ef1642c70cd5	warehouse_from_ids	-2147483621	2026-01-29 08:06:19.27726+07	\N	3
9262e660-7dd6-42dd-b68e-4554895a9b12	Parameter @start_date	5e08d675-2475-41c3-bf8e-ef1642c70cd5	start_date	21	2026-01-29 08:06:19.277282+07	NpgsqlDbType.Timestamp	4
eb4abc64-d880-4bcb-9a33-b745441e98cb	Parameter @end_date	5e08d675-2475-41c3-bf8e-ef1642c70cd5	end_date	21	2026-01-29 08:06:19.277299+07	NpgsqlDbType.Timestamp	5
7ea6b7cb-fa53-4751-a457-6b4bceddeb43	Parameter @page_size	5e08d675-2475-41c3-bf8e-ef1642c70cd5	page_size	9	2026-01-29 08:06:19.278155+07	NpgsqlDbType.Integer	6
de4b135e-3cd1-4070-a677-a54c7ec628d2	Parameter @page_index	5e08d675-2475-41c3-bf8e-ef1642c70cd5	page_index	13	2026-01-29 08:06:19.279045+07	NpgsqlDbType.Numeric	7
b6ae7a78-9d88-4ec7-9701-1161fef6d69d	Parameter @keyword	8f374573-5b28-4ba4-a298-be5b44d0cba8	keyword	19	2026-01-30 02:56:42.853776+07	NpgsqlDbType.Text	0
75c66874-7979-4a95-9f43-dafa3d450769	Parameter @permissions	8f374573-5b28-4ba4-a298-be5b44d0cba8	permissions	-2147483639	2026-01-30 11:28:17.921948+07	\N	1
6e3dbc83-bed1-4aaa-9019-97c04bd5d440	Parameter @brand_ids	042cb5a7-8c2a-4241-92ce-638ecf30fca8	brand_ids	-2147483621	2026-01-05 02:53:22.011+07	\N	5
beee65bd-b1c5-41df-b3a6-e36173da3619	Parameter @statuses	042cb5a7-8c2a-4241-92ce-638ecf30fca8	statuses	-2147483639	2026-01-30 10:48:41.060755+07	\N	6
ccce5d51-1e57-40be-b52e-f6dd0c973c48	Parameter @page_size	8f374573-5b28-4ba4-a298-be5b44d0cba8	page_size	9	2026-01-30 02:56:42.854347+07	NpgsqlDbType.Integer	2
09bbca34-b929-49d5-ad87-5ad7cd42b191	Parameter @page_index	8f374573-5b28-4ba4-a298-be5b44d0cba8	page_index	9	2026-01-30 02:56:42.85575+07	NpgsqlDbType.Integer	3
525f9cea-f91f-4dd8-adc0-bfa58ddc346d	Parameter @account_id	9bdb7431-3b20-4628-8d85-c9dcc0e1216b	account_id	27	2026-02-02 06:36:17.83096+07	NpgsqlDbType.Uuid	0
df7726ac-89d0-4683-ae67-448ea4eedf6e	Parameter @keyword	5e08d675-2475-41c3-bf8e-ef1642c70cd5	keyword	22	2026-01-29 08:06:19.279+07	NpgsqlDbType.Varchar	8
68f3a3f3-d271-43b8-962f-a352bf2b6c59	Parameter @group_ids	a697e112-5af5-4504-a92d-e795ef15db44	group_ids	-2147483621	2026-02-10 10:03:57.924328+07	\N	0
1c4d94c7-a9b8-4fdf-9c67-2bb9d0729767	Parameter @batch_id	5e08d675-2475-41c3-bf8e-ef1642c70cd5	batch_ids	-2147483621	2026-01-29 08:06:19.277171+07	\N	0
0e568166-64e5-47ae-9139-ee40e2791b6d	Parameter @product_ids	e74f092a-610e-4732-91f4-0ddf46e80f4e	product_ids	-2147483621	2026-02-02 15:50:12.967591+07	\N	1
ffa68c3e-f047-44dd-99b8-b73ea497bffe	Parameter @dealer_to_ids	e74f092a-610e-4732-91f4-0ddf46e80f4e	dealer_to_ids	-2147483621	2026-02-02 15:50:12.997085+07	\N	2
cbabca9e-dd1c-4d3d-a650-3085cfa48999	Parameter @warehouse_from_ids	e74f092a-610e-4732-91f4-0ddf46e80f4e	warehouse_from_ids	-2147483621	2026-02-02 15:50:13.161693+07	\N	3
ca00e2d9-42fe-4002-be43-195e16a924ce	Parameter @start_date	e74f092a-610e-4732-91f4-0ddf46e80f4e	start_date	21	2026-02-02 15:50:13.196222+07	NpgsqlDbType.Timestamp	4
2424da34-9da0-47cf-b257-d5d8e2f11d0a	Parameter @end_date	e74f092a-610e-4732-91f4-0ddf46e80f4e	end_date	21	2026-02-02 15:50:13.259892+07	NpgsqlDbType.Timestamp	5
62ed68b7-c3bf-4934-9d93-c6159569c49f	Parameter @page_size	e74f092a-610e-4732-91f4-0ddf46e80f4e	page_size	9	2026-02-02 15:50:13.287479+07	NpgsqlDbType.Integer	6
b01a277b-b9e6-4a49-9ab0-6cd5e1563192	Parameter @page_index	e74f092a-610e-4732-91f4-0ddf46e80f4e	page_index	9	2026-02-02 15:50:13.483682+07	NpgsqlDbType.Integer	0
265b57ba-dd36-40ef-9c03-96b1da340f07	Parameter @keyword	e74f092a-610e-4732-91f4-0ddf46e80f4e	keyword	22	2026-02-02 15:50:13.510314+07	NpgsqlDbType.Varchar	7
be37c8c5-e2d2-4278-ab1d-5aae3f1f5c1f	Parameter @keyword	56badf92-c3e2-4ecf-91af-337d8af5f594	keyword	19	2026-02-02 09:00:03.32701+07	NpgsqlDbType.Text	1
126657f5-c137-4d09-9249-a18605b7060a	Parameter @page_size	56badf92-c3e2-4ecf-91af-337d8af5f594	page_size	9	2026-02-02 09:00:03.329821+07	NpgsqlDbType.Integer	2
5126d4b2-8ab1-4123-8ff9-cd47efc6e3dd	Parameter @page_index	56badf92-c3e2-4ecf-91af-337d8af5f594	page_index	13	2026-02-02 09:00:03.332517+07	NpgsqlDbType.Numeric	3
ff2d8fd9-c4dc-4af1-81be-d5c3404bdfa3	Parameter @account_group_id	56badf92-c3e2-4ecf-91af-337d8af5f594	account_group_id	27	2026-02-02 09:00:03.323108+07	NpgsqlDbType.Uuid	0
a23ebc76-7beb-48a0-99f3-4eb0bb494d05	Parameter @product_ids	68f059e7-3190-4541-91a5-b4f44d0d08bb	product_ids	-2147483621	2026-02-03 10:50:04.858507+07	\N	0
032ac91d-59d1-4374-ab2a-86b0e27e729b	Parameter @warehouse_to_ids	68f059e7-3190-4541-91a5-b4f44d0d08bb	warehouse_to_ids	-2147483621	2026-02-03 10:50:04.949443+07	\N	0
20525abd-4c71-4a1e-a03b-febb22be3563	Parameter @start_date	68f059e7-3190-4541-91a5-b4f44d0d08bb	start_date	21	2026-02-03 10:50:05.038308+07	NpgsqlDbType.Timestamp	0
6fd6769b-2b54-4e2e-a612-dbcbf9356e13	Parameter @end_date	68f059e7-3190-4541-91a5-b4f44d0d08bb	end_date	21	2026-02-03 10:50:05.133186+07	NpgsqlDbType.Timestamp	0
33d52230-5e18-48be-af19-bba54ae3e3ec	Parameter @page_size	68f059e7-3190-4541-91a5-b4f44d0d08bb	page_size	9	2026-02-03 10:50:05.1588+07	NpgsqlDbType.Integer	0
58a8bda5-6e72-45ed-87f3-a22f85a8dd56	Parameter @page_index	68f059e7-3190-4541-91a5-b4f44d0d08bb	page_index	13	2026-02-03 10:50:05.232892+07	NpgsqlDbType.Numeric	0
70f09a7f-5fa0-488b-9c8a-781c8619256b	Parameter @batch_id	68f059e7-3190-4541-91a5-b4f44d0d08bb	batch_ids	-2147483621	2026-02-03 10:50:05.334129+07	\N	0
7e1c13de-a558-4e75-a98d-991e3a7f53c2	Parameter @keyword	68f059e7-3190-4541-91a5-b4f44d0d08bb	keyword	22	2026-02-03 10:50:05.259969+07	NpgsqlDbType.Varchar	0
2f12083f-0553-426a-aadd-eac95b02b597	Parameter @account_group_id	448d7ff1-4b06-42ce-8e9c-f3d25a43466a	account_group_id	27	2026-02-03 14:56:51.351977+07	NpgsqlDbType.Uuid	0
270da8ce-3bd2-4259-baaa-ae5f837a94b0	Parameter @page_index	448d7ff1-4b06-42ce-8e9c-f3d25a43466a	page_index	13	2026-02-03 06:44:42.143765+07	NpgsqlDbType.Numeric	3
06f8a48d-2d0d-4f27-a491-e7b57c694632	Parameter @page_size	448d7ff1-4b06-42ce-8e9c-f3d25a43466a	page_size	9	2026-02-03 06:44:42.142691+07	NpgsqlDbType.Integer	2
f57ab788-d15c-46ef-bd4f-b793399d52de	Parameter @keyword	448d7ff1-4b06-42ce-8e9c-f3d25a43466a	keyword	19	2026-02-03 06:44:42.141704+07	NpgsqlDbType.Text	1
2760044b-769f-4187-8ae4-6246a871962a	Parameter @account_types	c02b83c7-019d-48c6-83d5-42f2ba192f2f	account_types	-2147483639	2026-02-03 08:40:18.26142+07	\N	0
72a366b7-4d24-4667-b8d5-e0284dbf18f1	Parameter @keyword	0a535f72-d6e7-4250-9205-0e9b5dbbe7d6	keyword	19	2026-02-05 03:11:38.306609+07	NpgsqlDbType.Text	0
3024d240-c263-49bb-8af0-1b031563df68	Parameter @page_size	0a535f72-d6e7-4250-9205-0e9b5dbbe7d6	page_size	9	2026-02-05 03:11:38.30853+07	NpgsqlDbType.Integer	2
fc7ce2ed-6ace-4fc7-bc7d-d10cc2fe06f6	Parameter @page_index	0a535f72-d6e7-4250-9205-0e9b5dbbe7d6	page_index	13	2026-02-05 03:11:38.309285+07	NpgsqlDbType.Numeric	3
c6a4b6e8-1486-499f-b737-7d6ed02a1391	Parameter @parent_account_id	0a535f72-d6e7-4250-9205-0e9b5dbbe7d6	parent_account_id	27	2026-02-05 03:11:38.307739+07	NpgsqlDbType.Uuid	1
ffa05b42-4fe4-4164-af41-99722bc135e1	Parameter @ids	764766da-1dda-4c74-ac3d-68e533fb5b6d	ids	-2147483621	2026-02-06 07:08:17.34354+07	\N	0
\.


--
-- Data for Name: formula_config; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.formula_config (id, table_name, table_column, code, prefix, current_value, suffix, created_at, updated_at, formula_id) FROM stdin;
4972103b-03b7-4987-a863-5cbe45b8e128	\N	\N	AccGenABC	\N	A	\N	2026-02-09 18:02:40.743859+07	\N	a767c54a-a0a8-4eeb-a4af-42233bcd4318
266f5d76-ad5e-4eea-b737-6c77c5a2c72b	\N	\N	AccGenABC.29	29	Q	\N	2026-02-25 17:57:02.333922+07	2026-02-26 09:55:21.535449+07	a767c54a-a0a8-4eeb-a4af-42233bcd4318
9f46b8ef-6db7-4f5d-bebb-9e223f286bef	\N	\N	AccountProfessorGen	\N	\N	\N	2026-02-26 14:25:08.277554+07	\N	9d327381-b815-46db-b829-e6f37709f094
1762ecbe-e77b-4060-9ae9-5f73b4bc23b7	\N	\N	AccountProfessorGen.29	29	L	\N	2026-02-25 17:25:26.878124+07	2026-02-26 07:54:50.374298+07	9d327381-b815-46db-b829-e6f37709f094
\.


--
-- Data for Name: generic_formula; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.generic_formula (id, fomula_name, block_type, data_type, start_value_text, end_value_text, current_value_text, regex_text, components, logic_type) FROM stdin;
a35c67d3-8a91-4082-9134-9eb3f9f9b10f	ComplexHandler	4	4	\N	\N	\N	(new string(((((({{cdfbab9c-3f3d-43a6-8c37-06f00e6990b9}}) + {{ddd6c45e-3edd-4f72-bf0f-c03957e3c0f8}}) + {{81b29a12-c845-4261-8c4e-74bdca2f1688}}) + {{d2890706-c4b3-4068-bf7f-4bce9100fdd3}}) + {{1f6e2a0a-d3f1-4840-9f12-9a5a39325504}}).Reverse().ToArray())).ToUpper()	[{"FormulaComponentId":"cdfbab9c-3f3d-43a6-8c37-06f00e6990b9","SortOrder":1},{"FormulaComponentId":"ddd6c45e-3edd-4f72-bf0f-c03957e3c0f8","SortOrder":2},{"FormulaComponentId":"81b29a12-c845-4261-8c4e-74bdca2f1688","SortOrder":3},{"FormulaComponentId":"d2890706-c4b3-4068-bf7f-4bce9100fdd3","SortOrder":4},{"FormulaComponentId":"1f6e2a0a-d3f1-4840-9f12-9a5a39325504","SortOrder":5},{"FormulaComponentId":"cc43946e-6a51-4ef4-bcb7-6975b9771989","SortOrder":6},{"FormulaComponentId":"5a0048ca-d3ed-45ac-8acd-e06ff056373d","SortOrder":7}]	0
cdfbab9c-3f3d-43a6-8c37-06f00e6990b9	Int 0-9	1	2	0	9	\N	^[0-9]+$	\N	0
ddd6c45e-3edd-4f72-bf0f-c03957e3c0f8	Char A-Z	1	1	A	Z	\N	^[A-Z]+$	\N	0
81b29a12-c845-4261-8c4e-74bdca2f1688	Int 2-8	1	2	2	8	\N	^[2-8]+$	\N	0
d2890706-c4b3-4068-bf7f-4bce9100fdd3	Char B-Q	1	1	B	Q	\N	^[B-Q]+$	\N	0
1f6e2a0a-d3f1-4840-9f12-9a5a39325504	Alphabet ABCDEFGH	1	4	\N	\N	ABCDEFGH	^[ABCDEFGH]+$	\N	0
cc43946e-6a51-4ef4-bcb7-6975b9771989	ReverseLogic	2	4	\N	\N	\N	input => new string(input.Reverse().ToArray())	\N	1
5a0048ca-d3ed-45ac-8acd-e06ff056373d	UpperLogic	2	4	\N	\N	\N	input => input.ToUpper()	\N	2
a767c54a-a0a8-4eeb-a4af-42233bcd4318	Gen(A-Z) Handler	4	4	\N	\N	\N	{{ddd6c45e-3edd-4f72-bf0f-c03957e3c0f8}}	[{"FormulaComponentId":"ddd6c45e-3edd-4f72-bf0f-c03957e3c0f8","SortOrder":1}]	0
e7d6602b-e014-473f-8c47-dbd67578e575	Int 0-99999999	1	2	0	99999999	\N	^[0-99999999]+$	\N	0
b6b8734f-34c4-4ac1-a4af-218bc0a5107e	Gen(0-99999999) Handler	4	4	\N	\N	\N	{{cdfbab9c-3f3d-43a6-8c37-06f00e6990b9}}	[{"FormulaComponentId":"e7d6602b-e014-473f-8c47-dbd67578e575","SortOrder":1}]	0
506760b5-d3dd-4591-8e38-77b5027a5c14	test	4	4	a	g	\N	((({{e7d6602b-e014-473f-8c47-dbd67578e575}}) + {{ddd6c45e-3edd-4f72-bf0f-c03957e3c0f8}}) + {{d2890706-c4b3-4068-bf7f-4bce9100fdd3}}) + {{cdfbab9c-3f3d-43a6-8c37-06f00e6990b9}}	[{"FormulaComponentId":"e7d6602b-e014-473f-8c47-dbd67578e575","FormulaComponentName":"Int 0-99999999","SortOrder":1},{"FormulaComponentId":"ddd6c45e-3edd-4f72-bf0f-c03957e3c0f8","FormulaComponentName":"Char A-Z","SortOrder":2},{"FormulaComponentId":"d2890706-c4b3-4068-bf7f-4bce9100fdd3","FormulaComponentName":"Char B-Q","SortOrder":3},{"FormulaComponentId":"cdfbab9c-3f3d-43a6-8c37-06f00e6990b9","FormulaComponentName":"Int 0-9","SortOrder":4}]	5
9d327381-b815-46db-b829-e6f37709f094	Tạo tài khoản (A-Z)	4	4	\N	\N	\N	{{ddd6c45e-3edd-4f72-bf0f-c03957e3c0f8}}	[{"FormulaComponentId":"ddd6c45e-3edd-4f72-bf0f-c03957e3c0f8","FormulaComponentName":"Char A-Z","SortOrder":1}]	0
\.


--
-- Data for Name: tblmaster; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.tblmaster (id, code, description, execfunc, query, exectype, created_at, service_name, db_type) FROM stdin;
a029a5aa-1e2b-4f7a-a828-563ec3ef898f	account_exception_controller_get_by_account_id_and_controller_codes	account_exception_controller_get_by_account_id_and_controller_codes	\N	SELECT e.*\r\nFROM acc_srv_account_permission_exception e\r\nJOIN (\r\n    SELECT unnest(@controller_codes::varchar[]) AS code\r\n) c\r\n    ON e.acc_srv_account_permission_controller_code = c.code\r\nWHERE e.acc_srv_account_id = @account_id;\r\n	1	2025-12-13 16:54:34.416095+07	AccountService.AccountPermissionService	3
8c18f93d-629e-4d4c-a77c-3f555c9574a6	survey_created_at	Danh sách khảo sát đang mở	\N	SELECT * FROM (\r\n    SELECT\r\n        s.id AS survey_id,\r\n        s.title AS survey_title,\r\n        s.description AS survey_description,\r\n        s.created_at AS survey_created_at,\r\n        s.expired_at AS survey_expired_at\r\n    FROM ques_srv_survey s\r\n    WHERE s.is_published = TRUE\r\n      AND (s.expired_at IS NULL OR s.expired_at > NOW())\r\n) AS published_surveys\r\nORDER BY survey_created_at desc	1	2025-11-20 23:29:03.30255+07	QuestionService	3
fecf2920-4633-469f-a6af-bdf06149b58a	sp_dyn_patient_rank_search	\N	sp_dyn_patient_rank_search	\N	2	2025-11-24 17:18:11.96618+07	QuestionService	3
56d1fe5c-72eb-41fc-88b1-94b2963a6df9	countries_get_by_ids	countries_get_by_ids	\N	select e.*\r\nFROM mlg_srv_country e\r\nJOIN (\r\n    SELECT unnest(@country_ids::uuid[]) AS id\r\n) c\r\n    ON e.id = c.id\r\nwhere 0 = 0	1	2025-12-16 15:52:12.543221+07	HCare.MultilingualismService.LocationService	3
06bbdd03-e15e-4e66-8c3a-586469446a59	survey_calc_score	Tính tổng điểm khảo sát của user	ques_srv_get_user_survey_score	SELECT sum(t.score) AS total_score\r\nFROM (\r\n\t\t(\r\n\t\t    SELECT user_answer.score_text_answer AS score\r\n\t\t    FROM ques_srv_survey survey,\r\n\t\t    \tques_srv_question question,\r\n\t\t    \tques_srv_user_answer user_answer\r\n\t    \tWHERE user_answer.ques_srv_user_id = @user_id\r\n\t    \t\tand survey.id = @survey_id\r\n\t    \t\tand user_answer.ques_srv_question_id = question.id\r\n\t    \t\tand question.ques_srv_survey_id = survey.id\r\n\t\t)\r\n\t\tUNION ALL\r\n\t\t(\r\n\t\t    SELECT answer.score AS score\r\n\t    \tFROM ques_srv_user_answer user_answer,\r\n\t    \t\tques_srv_survey survey,\r\n\t    \t\tques_srv_question question,\r\n\t    \t\tques_srv_answer answer\r\n\t\t    WHERE answer.id = any(user_answer.ques_srv_answer_ids)\r\n\t\t\t\tand user_answer.ques_srv_user_id = @user_id\r\n\t\t\t\tand survey.id = @survey_id\r\n\t\t\t\tand question.ques_srv_survey_id  = survey.id\r\n\t\t\t\tand question.id = answer.ques_srv_question_id\r\n\t\t)\r\n) AS t	1	2025-11-25 00:09:24.991156+07	SurveyService	3
34b687b8-f94a-4101-a5f5-4f4640d112d3	sp_dynamic_patient_visit_sreach	Sreach bệnh nhân khám	sp_dynamic_patient_visit_sreach	\N	2	2025-12-09 16:41:21.230193+07	MedicalService.PatientVisitService	3
a3c0026e-23ae-44a5-ae88-787a3ed44052	user_answer_add	Lưu trữ câu trả của người dùng khi tích chọn hoặc hoàn thành đoạn text	\N	INSERT INTO public.ques_srv_survey (\r\n    title,\r\n    description,\r\n    is_published,\r\n    expired_at\r\n)\r\nVALUES (\r\n    @title,\r\n    @description,\r\n    @is_published,\r\n    @expired_at\r\n)	1	2025-11-24 20:48:51.509184+07	QuestionService	3
808ca6a9-d84e-4401-a5dd-5cd0fe132342	account_permission_controller_get_by_codes	account_permission_controller_get_by_codes	\N	SELECT e.*\r\nFROM acc_srv_account_permission_controller e\r\nJOIN (\r\n    SELECT unnest(@codes::varchar[]) AS code\r\n) c\r\n    ON e.code = c.code\r\nWHERE 0 = 0	1	2025-12-14 13:26:42.044478+07	AccountService.AccountPermissionService	3
a77aaa63-dc1e-48b0-9409-5445d2c1e0e6	user_answer_update	Cập nhật lưu trữ câu trả của người dùng khi tích chọn hoặc hoàn thành đoạn text	\N	UPDATE public.ques_srv_survey\r\nSET\r\n    title = @title,\r\n    description = @description,\r\n    is_published = @is_published,\r\n    expired_at = @expired_at,\r\n    updated_at = CURRENT_TIMESTAMP\r\nWHERE\r\n    id = @id	1	2025-11-24 22:16:31.681997+07	QuestionService	3
4d599b40-2e94-40f6-881f-289d890b7076	survey_get_answers_score	Tính tổng điểm của các câu trả lời đã chọn	\N	SELECT COALESCE(SUM(score), 0)\r\nFROM public.ques_srv_answer\r\nWHERE id = ANY(@answer_ids);	1	2025-11-29 22:59:27.314048+07	QuestionService	3
4f310ed1-0907-4e49-b1ef-ad4bf7ab0cfd	locales_get_by_ids	locales_get_by_ids	\N	SELECT e.*\r\nFROM mlg_srv_locale e\r\nJOIN (\r\n    SELECT unnest(@locale_ids::uuid[]) AS id\r\n) c\r\n    ON e.id = c.id\r\nWHERE 0 = 0	1	2025-12-16 23:11:22.758484+07	HCare.MultilingualismService.LocaleService	3
39f2d3e4-a6a6-4db8-aed5-ce35a3892550	locales_get_by_lang_code	locales_get_by_lang_code	\N	SELECT *,\r\n       COUNT(*) OVER() AS total_rows\r\nFROM mlg_srv_locale\r\nWHERE mlg_srv_country_lang_code = @lang_code\r\nORDER BY created_at\r\nOFFSET (@page_index * @page_size) ROWS\r\nFETCH NEXT @page_size ROWS ONLY	1	2025-12-16 23:28:27.217332+07	HCare.MultilingualismService.LocaleService	3
4583025a-5dfd-428b-89a1-a3451ef6984d	account_login_info_get_by_session_id	account_login_info_get_by_session_id	\N	select * from acc_srv_account_login where session_id = @session_id	1	2025-11-27 23:24:58.938801+07	AccountService.AuthenService	3
f31f7385-4dd0-412e-921d-bf1bde1bfe05	account_login_info_get_by_refresh_token	account_login_info_get_by_refresh_token	\N	select * from acc_srv_account_login where refresh_token = @refresh_token	1	2025-11-27 23:06:55.291155+07	AccountService.AuthenService	3
d2b894c4-ccb6-4437-af9e-4254f063dac6	account_login_info_get_by_account_id	account_login_info_get_by_account_id	\N	select * from acc_srv_account_login where acc_srv_account_id = @account_id	1	2025-11-27 23:11:50.346879+07	AccountService.AuthenService	3
3b17d2aa-07e2-4ec6-a204-b587b504889a	account_get_by_id	account_get_by_id	\N	select * from acc_srv_account where id = @id	1	2025-11-28 17:00:26.866032+07	AccountService.AuthenService	3
82cbb8ca-23b5-4d4a-8151-a0f05a7844cc	survey_insert_user_answer	Lưu câu trả lời chi tiết của người dùng vào ques_srv_user_answer	\N	INSERT INTO public.ques_srv_user_answer (ques_srv_user_id, ques_srv_question_id, ques_srv_answer_ids, text_answer, score_text_answer, ques_srv_user_survey_result_id) VALUES (@user_id, @question_id, @answer_ids, @text_answer, @score_text_answer, @result_id);	1	2025-11-25 00:09:24.991156+07	QuestionService	3
b3a30e8e-8aad-4894-8c6e-d824f8d692c6	account_get_by_username_and_password	account_get_by_username_and_password	\N	SELECT *\r\nFROM acc_srv_account\r\nWHERE ((LOWER(username) = LOWER(@username)) OR (phone_number = @username))\r\n  AND password = @password	1	2025-11-27 15:41:30.957438+07	AccountService.AuthenService	3
128f26a2-6324-4749-82f3-39fd1a022009	locale_get_by_lang_code_and_resource_keys	locale_get_by_lang_code_and_resource_keys	\N	SELECT e.*\r\nFROM mlg_srv_locale e\r\nJOIN (\r\n    SELECT unnest(@resource_keys::varchar[]) AS resource_key\r\n) c\r\n    ON e.resource_key = c.resource_key\r\nWHERE e.mlg_srv_country_lang_code = @lang_code and e.resource_module = @resource_module\r\n	1	2025-12-16 23:30:23.059427+07	HCare.MultilingualismService.LocaleService	3
2413277c-6254-41aa-86fd-18bc6c2e79b2	account_get_by_username_or_phone_number_or_email	account_get_by_username_or_phone_number_or_email	\N	SELECT *\r\nFROM acc_srv_account\r\nWHERE (LOWER(username) = LOWER(@username) \r\n       OR LOWER(phone_number) = LOWER(@phone_number) \r\n       OR LOWER(email) = LOWER(@email))	1	2025-12-04 14:44:52.666638+07	AccountService.AuthenService	3
5b11c553-a2f1-4a55-8c6f-9103f31fa7f1	survey_result_insert	Chèn kết quả tổng quan và trả về ID	\N	INSERT INTO public.ques_srv_user_survey_result (ques_srv_user_id, ques_srv_survey_id, total_score, max_possible_score, outcome_data) VALUES (@user_id, @survey_id, @total_score, @max_score, @outcome_data::jsonb) RETURNING id;	1	2025-11-29 22:59:27.314048+07	QuestionService	3
da1d4e7f-0090-409d-ae02-10c4a8d95343	product_get_by_ids	product_get_by_ids	\N	SELECT e.*\r\nFROM crm_srv_product e\r\nJOIN (\r\n    SELECT unnest(@product_ids::uuid[]) AS product_id\r\n) c\r\n    ON e.id = c.product_id\r\nWHERE 0=0	1	2026-01-02 10:56:15.772659+07	HCare.CrmService.ProductService	3
66a5c83d-6d95-4ca1-8b9a-10e56d1a69db	survey_get_max_score	Tính điểm tối đa có thể có của một survey	\N	SELECT COALESCE(SUM(max_val), 0)\r\nFROM (\r\n    SELECT MAX(a.score) as max_val\r\n    FROM public.ques_srv_question q\r\n    JOIN public.ques_srv_answer a ON q.id = a.ques_srv_question_id\r\n    WHERE q.ques_srv_survey_id = @survey_id\r\n    GROUP BY q.id\r\n) sub;	1	2025-11-29 22:59:27.314048+07	QuestionService	3
93099ec1-23bc-49cf-9083-1638f215a4ab	get_by_user_or_survey_history	Lấy lịch sử khảo sát theo user_id hoặc survey_id	ques_srv_get_by_user_or_survey_history	\N	2	2025-11-28 11:34:00.200659+07	SurveyService	3
caa66f91-075f-4297-a5eb-088d39e4a2cc	get_survey_outcomes_by_survey_id_and_score	Đối chiếu kết quả khảo sát và điểm khảo sát	\N	select\r\n\tid,\r\n\tques_srv_survey_id,\r\n\tmin_score,\r\n\tmax_score,\r\n\tcondition_label,\r\n\tresult_title,\r\n\tresult_description,\r\n\trecommendation\r\nfrom ques_srv_survey_outcome where ques_srv_survey_id = @ques_srv_survey_id and @total_score between min_score and max_score\r\n	1	2025-11-26 14:47:32.149115+07	SurveyService	3
f092152c-5fe0-4315-873d-e247e72020b9	questions_by_survey_id	Lấy câu hỏi kèm bộ câu trả lời cho từng câu hỏi với Id từ bộ câu hỏi (Survey)	\N	select * from (\r\n\tselect\r\n\t\tsurvey.id as survey_id,\r\n\t\tsurvey.title as survey_title,\r\n\t\tsurvey.description as survey_description,\r\n\t\tsurvey.is_published as survey_is_published,\r\n\t\tsurvey.created_at as survey_created_at,\r\n\t\tsurvey.expired_at as survey_expired_at,\r\n\t\tsurvey.updated_at as survey_updated_at,\r\n\t\tquestion.id as question_id,\r\n\t\tquestion.question_text as question_question_text,\r\n\t\tquestion.question_order as question_question_order,\r\n\t\tquestion.is_required as question_is_required,\r\n\t\tquestion.created_at as question_created_at,\r\n\t\tquestion.updated_at as question_updated_at,\r\n\t\tanswer.id as answer_id,\r\n\t\tanswer.description as answer_description,\r\n\t\tanswer.order_sort as answer_order_sort,\r\n\t\tanswer.score as answer_score,\r\n\t\tanswer.created_at as answer_created_at\r\n\tfrom ques_srv_survey survey, ques_srv_question question, ques_srv_answer answer\r\n\t\twhere survey.id = question.ques_srv_survey_id and question.id = answer.ques_srv_question_id\r\n\t\t\tand survey.id = @survey_id\r\n) as questions_by_survey_id	1	2025-11-20 21:22:59.299194+07	SurveyService	3
3c00c6b9-13be-4600-a7b3-27cefe00f54a	account_exception_controller_delete_old_version_by_host_code	account_exception_controller_delete_old_version_by_host_code	\N	DELETE FROM acc_srv_account_permission_controller WHERE host_code = @host_code AND version_id <> @version_id	1	2025-12-13 15:15:14.131942+07	AccountService.AccountPermissionService	3
51acad4e-9f4a-4d5f-930d-a154085775ee	account_exception_controller_get_by_account_id	account_exception_controller_get_by_account_id	\N	SELECT *\r\n\tFROM acc_srv_account_permission_exception\r\n\tWHERE acc_srv_account_id = @account_id	1	2025-12-14 15:50:27.900663+07	AccountService.AccountPermissionService	3
536be301-e81d-45ba-9573-e203f76f1aab	locales_get_by_resource_key	locales_get_by_resource_key	\N	SELECT * FROM mlg_srv_locale where resource_key = @resource_key	1	2025-12-16 23:34:30.081551+07	HCare.MultilingualismService.LocaleService	3
8712e369-cfab-4567-8c41-229be494aa15	province_get_by_id	province_get_by_id	\N	\r\nselect pr.*\r\nfrom mlg_srv_province pr\r\nwhere pr.id = @province_id	1	2025-12-22 15:44:09.362255+07	HCare.MultilingualismService.LocationService	3
efe8ec4e-4c7d-4e5f-8d55-081e0f8023a4	ensure_locale_partitions	ensure_locale_partition	\N	CALL ensure_locale_partitions(@lang_codes)	1	2025-12-18 13:40:51.853793+07	HCare.MultilingualismService.LocaleService	3
189d19a8-794b-412a-8b3a-3f30bc8d0ffa	account_get_by_ids	account_get_by_ids	\N	select e.*\r\nFROM acc_srv_account e\r\nJOIN (\r\n    SELECT unnest(@ids::uuid[]) AS id\r\n) c\r\n    ON e.id = c.id\r\nwhere 0 = 0	1	2025-12-19 10:57:20.422781+07	AccountService.AuthenService	3
9b8c8408-cf9e-461a-ab99-794a47e3960f	account_group_get_by_group_ids	account_group_get_by_group_ids	\N	SELECT e.*\r\nFROM acc_srv_account_group e\r\nJOIN (\r\n    SELECT unnest(@group_ids::uuid[]) AS id\r\n) c\r\n    ON e.id = c.id\r\nWHERE 1 = 1\r\nORDER BY e.id, created_at\r\nLIMIT @page_size OFFSET (@page_index * @page_size)	1	2025-12-19 12:00:55.600643+07	AccountService.AccountGroupService	3
a697e112-5af5-4504-a92d-e795ef15db44	account_group_permission_get_list_code_by_group_ids	lấy danh sách code theo list group id	\N	SELECT DISTINCT ON (acc_srv_account_permission_controller_code) *\r\nFROM public.acc_srv_account_group_permission_controller\r\nWHERE acc_srv_account_group_id = ANY(@group_ids::uuid[])\r\nORDER BY acc_srv_account_permission_controller_code, created_at DESC;	1	2026-02-10 10:01:13.571099+07	HCare.AccountService.PermissionControllerService	3
cd26799d-ac81-41c2-83ba-1d7eed239c50	account_login_info_to_hist	account_login_info_to_hist	\N	WITH moved AS (\r\n    DELETE FROM public.acc_srv_account_login\r\n    WHERE token_status = 0\r\n      AND (\r\n          refresh_token_date < NOW()\r\n          OR token_status = 0\r\n      )\r\n    RETURNING *\r\n)\r\nINSERT INTO public.acc_srv_account_login_hist\r\nSELECT * FROM moved;\r\n\r\nUPDATE public.acc_srv_account_login\r\nSET last_sync_date = NOW();\r\n\r\n	1	2026-02-05 02:34:25.802+07	AccountService.AuthenService	3
39573707-0323-4dfc-a752-6a9c1c715ddc	account_addresses_get_by_ids	account_addresses_get_by_ids	\N	select e.*\r\nFROM acc_srv_account_address e\r\nJOIN (\r\n    SELECT unnest(@ids::uuid[]) AS id\r\n) c\r\n    ON e.id = c.id\r\nwhere 0 = 0\r\n	1	2025-12-25 16:31:12.685955+07	HCare.AccountService.AccountInfoService	3
0bee804a-0ca9-4889-87ba-0a692ee97988	account_info_active_get_by_id	account_info_active_get_by_id	\N	select * from acc_srv_account_info where acc_srv_account_id = @account_id and status = 1	1	2025-12-12 11:17:27.903359+07	AccountService.AuthenService	3
cc3ab878-8a7e-4994-84b3-c0eaad0e7ea3	account_info_active_get_by_ids	account_info_active_get_by_ids	\N	SELECT e.*\r\nFROM acc_srv_account_info e\r\nJOIN (\r\n    SELECT unnest(@account_ids::uuid[]) AS accountId\r\n) c\r\n    ON e.acc_srv_account_id = c.accountId\r\nWHERE status = 1 	1	2025-12-26 07:48:42.966423+07	HCare.AccountService.AuthenticationService	3
49c95e57-13ba-4cba-ac25-ed156f31c377	account_addresses_default_get_by_account_ids	account_addresses_default_get_by_account_ids	\N	SELECT e.*\r\nFROM acc_srv_account_address e\r\nJOIN (\r\n    SELECT unnest(@account_ids::uuid[]) AS accountId\r\n) c\r\n    ON e.acc_srv_account_id = c.accountId\r\nWHERE address_type = 1	1	2025-12-26 09:22:32.462851+07	HCare.AccountService.AuthenticationService	3
cb7c46a3-c40b-437e-a01b-73cbbb5174d4	dealer_get_by_ids	dealer_get_by_ids	\N	select e.*\r\nFROM crm_srv_dealer e\r\nJOIN (\r\n    SELECT unnest(@dealer_ids::uuid[]) AS id\r\n) c\r\n    ON e.id = c.id\r\nwhere 0 = 0	1	2025-12-22 14:58:15.66642+07	Hcare.CrmService.DealerService	3
fa224726-46d0-49b2-9e85-383fcaaf9d33	account_group_get_first_by_group_ids	account_group_get_first_by_group_ids	\N	SELECT DISTINCT ON (e.acc_srv_account_leader_id, e.group_name) \r\n    e.*\r\nFROM acc_srv_account_group e\r\nJOIN (\r\n    SELECT unnest(@group_ids::uuid[]) AS id\r\n) c ON e.id = c.id\r\nWHERE 1 = 1\r\nORDER BY e.group_name, e.acc_srv_account_leader_id, e.acc_srv_account_member	1	2026-01-23 15:21:46.578346+07	AccountService.AccountGroupService	3
c0acc2a2-336f-4690-833a-cceb7ce9c2f0	account_dashboard_view_get_by_account_id	account_dashboard_view_get_by_account_id	\N	select * from vw_account_permission_extended_stats where account_id = @account_id	1	2026-02-10 14:40:20.571621+07	HCare.AccountService.AccountDashboardViewService	3
2544ae14-69c1-4d40-a160-590ab777ca9b	dealer_level_get_all	dealer_level_get_all	\N	\r\n SELECT *\r\nFROM crm_srv_dealer_level\r\nWHERE (@dealer_level_status is null or status = @dealer_level_status)\r\norder by priority	1	2025-12-31 11:46:46.738787+07	HCare.CrmService.DealerService	3
f4865b1d-1824-497e-8f43-b1888335e888	account_menu_get_by_id	account_menu_get_by_id	\N	select acc_menu.*\r\nfrom acc_srv_account_menu acc_menu\r\nwhere acc_menu.id = @id	1	2025-12-30 09:15:21.679634+07	Hcare.AccountService.AccountMenu	3
def96aa8-83c1-4b97-9c26-913302f9e2fd	account_menu_get_by_parent_id	account_menu_get_by_parent_id	\N	select acc_menu.*\r\nfrom acc_srv_account_menu acc_menu\r\nwhere acc_menu.parent_id = @parent_id;	1	2025-12-31 10:16:20.091183+07	HCare.AccountService.AccountMenu	3
9d880502-f2ad-41da-aa51-ac0c4ab00061	product_attribute_get_by_ids	product_attribute_get_by_ids	\N	SELECT e.*\r\nFROM crm_srv_product_attribute e\r\nJOIN (\r\n    SELECT unnest(@ids::uuid[]) AS id\r\n) c\r\n    ON e.id = c.id\r\nWHERE 0=0	1	2026-01-03 06:29:17.20627+07	HCare.CrmService.ProductAttributeService	3
0b33adc0-b348-492b-98d9-67a204e0b0d5	locales_search	locales_search	\N	WITH filtered AS (\r\n    SELECT *\r\n    FROM mlg_srv_locale\r\n    WHERE\r\n        (\r\n            @keyword IS NULL\r\n            OR resource_key ILIKE '%' || @keyword || '%'\r\n            OR resource_value ILIKE '%' || @keyword || '%'\r\n        )\r\n      AND (\r\n            @lang_code IS NULL\r\n            OR mlg_srv_country_lang_code = @lang_code\r\n        )\r\n      AND (\r\n      \t\t@resource_module IS NULL\r\n            OR resource_module = @resource_module\r\n      )\r\n)\r\nSELECT \r\n    f.*,\r\n    t.total_row\r\nFROM filtered f\r\nCROSS JOIN (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n) t\r\nORDER BY f.mlg_srv_country_lang_code, f.resource_key\r\nLIMIT @page_size OFFSET (@page_index * @page_size)	1	2025-12-18 14:55:38.353851+07	HCare.MultilingualismService.LocaleService	3
0b262339-226b-4246-bd61-9d35de6b518e	dealer_account_mapping_get_by_dealer_ids	dealer_account_mapping_get_by_dealer_ids	\N	SELECT e.*\r\nFROM crm_srv_dealer_account_mapping e\r\nJOIN (\r\n    SELECT unnest(@dealer_ids::uuid[]) AS dealer_id\r\n) c\r\n    ON e.prd_srv_dealer_id = c.dealer_id\r\nWHERE 0=0	1	2025-12-27 16:40:29.396658+07	Hcare.CrmService.DealerService	3
8cf74290-1174-4c4c-a177-591a8a2aa5c4	dealer_level_get_by_ids	dealer_level_get_by_ids	\N	SELECT e.*\r\nFROM crm_srv_dealer_level e\r\nJOIN (\r\n    SELECT unnest(@level_ids::uuid[]) AS level_id\r\n) c\r\n    ON e.id = c.level_id\r\nWHERE 0=0	1	2025-12-30 06:58:34.40149+07	Hcare.CrmService.DealerService	3
a4525bf6-d6e3-41ca-b8f2-b0b4f07bc843	product_attribute_get_by_product_ids	product_attribute_get_by_product_ids	\N	SELECT e.*\r\nFROM crm_srv_product_attribute e\r\nJOIN (\r\n    SELECT unnest(@product_ids::uuid[]) AS product_id\r\n) c\r\n    ON e.crm_srv_product_id = c.product_id\r\nWHERE 0=0	1	2026-01-03 06:30:24.589876+07	HCare.CrmService.ProductAttributeService	3
61c8e7ea-79e8-40d1-8d4c-f00eb81712fe	product_attribute_value_get_by_ids	product_attribute_value_get_by_ids	\N	SELECT e.*\r\nFROM crm_srv_product_attribute_value e\r\nJOIN (\r\n    SELECT unnest(@ids::uuid[]) AS id\r\n) c\r\n    ON e.id = c.id\r\nWHERE 0=0	1	2026-01-03 06:57:20.944464+07	HCare.CrmService.ProductAttributeService	3
89b6b21d-f6c3-4dc2-8929-813df27c2ead	product_get_by_parent_ids	product_get_by_parent_ids	\N	SELECT e.*\r\nFROM crm_srv_product e\r\nJOIN (\r\n    SELECT unnest(@product_parent_ids::uuid[]) AS product_parent_id\r\n) c\r\n    ON e.parent_id = c.product_parent_id\r\nWHERE 0=0	1	2026-01-03 07:23:01.169503+07	HCare.CrmService.ProductService	3
702c6f43-1480-45fe-933c-ee6bd3874c2d	dealer_search	dealer_search	\N	WITH filtered AS (\r\n    SELECT *\r\n    FROM crm_srv_dealer d\r\n    WHERE (\r\n            COALESCE(NULLIF(@parent_dealer_id, '00000000-0000-0000-0000-000000000000'), NULL) IS NULL\r\n            OR d.parent_dealer_id = @parent_dealer_id\r\n          )\r\n      AND (\r\n            COALESCE(NULLIF(@dealer_level_id, '00000000-0000-0000-0000-000000000000'), NULL) IS NULL\r\n            OR d.dealer_level_id = @dealer_level_id\r\n          )\r\n      AND (\r\n            @keyword IS NULL \r\n            OR d.name ILIKE '%' || @keyword || '%'\r\n            OR d.code ILIKE '%' || @keyword || '%'\r\n            OR d.address ILIKE '%' || @keyword || '%'\r\n            OR d.phone_number ILIKE '%' || @keyword || '%'\r\n            OR d.email ILIKE '%' || @keyword || '%'\r\n          )\r\n      AND status <> 0\r\n)\r\nSELECT f.*, t.total_row\r\nFROM filtered f\r\nCROSS JOIN (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n) t\r\nORDER BY f.created_at DESC\r\nLIMIT COALESCE(@page_size, 9223372036854775807)\r\nOFFSET COALESCE(@page_index, 0) * COALESCE(@page_size, 0)	1	2026-01-02 09:19:13.060607+07	HCare.CrmService.DealerService	3
323b13a2-3845-49e3-b191-3e28169ec340	account_menu_get_all	account_menu_get_all	\N	select acc_menu.*\r\nfrom acc_srv_account_menu acc_menu\r\nwhere acc_menu.menu_status = 1\r\norder by acc_menu.display_order;	1	2026-01-05 06:42:47.230277+07	HCare.AccountService.AccountMenu	3
03a817f2-bc6c-4e57-a855-ecf65516c2e4	product_attribute_value_get_by_attribute_ids	product_attribute_value_get_by_attribute_ids	\N	WITH filtered AS (\r\n    SELECT e.*\r\n    FROM crm_srv_product_attribute_value e\r\n    INNER JOIN unnest(@attribute_ids::uuid[]) AS c(attr_id)\r\n        ON e.crm_srv_product_attribute_id = c.attr_id\r\n    where e.status <> 0\r\n)\r\n, counted AS (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n)\r\nSELECT f.*, c.total_row\r\nFROM filtered f\r\nCROSS JOIN counted c\r\nORDER BY f.created_at DESC\r\nLIMIT @page_size  -- Nếu @page_size null, Postgres sẽ lấy ALL\r\nOFFSET (CASE \r\n            WHEN @page_size IS NULL OR @page_index IS NULL THEN 0 \r\n            ELSE @page_index * @page_size \r\n        END)	1	2026-01-05 03:11:15.703267+07	HCare.CrmService.ProductService	3
5f7834e4-5578-41ce-93bb-bbd531dff235	dealer_account_mapping_get_by_account_or_dealer	dealer_account_mapping_get_by_account_or_dealer	\N	SELECT *\r\nFROM crm_srv_dealer_account_mapping\r\nWHERE (@account_id IS NULL OR acc_srv_account_id = @account_id)\r\n  AND (@dealer_id IS NULL OR prd_srv_dealer_id = @dealer_id)\r\nLIMIT COALESCE(@page_size, 9223372036854775807)  -- BIGINT max\r\nOFFSET COALESCE(@page_index, 0) * COALESCE(@page_size, 0)	1	2026-01-01 15:38:27.367676+07	HCare.CrmService.DealerService	3
68f059e7-3190-4541-91a5-b4f44d0d08bb	warehouse_product_import_search	warehouse_product_import_search	\N	WITH filtered AS (\r\n    SELECT\r\n        pb.*\r\n    FROM\r\n        crm_srv_product_batch pb\r\n        INNER JOIN crm_srv_product p \r\n            ON pb.crm_srv_product_id = p.id\r\n        INNER JOIN crm_srv_warehouse w_to \r\n            ON pb.crm_srv_warehouse_id_to = w_to.id\r\n    WHERE\r\n        pb.is_active = TRUE\r\n        AND (pb.crm_srv_warehouse_id_from IS NULL \r\n             OR pb.crm_srv_warehouse_id_from = '00000000-0000-0000-0000-000000000000')\r\n        AND (\r\n\t\t\t    (@keyword IS NULL OR @keyword = '')\r\n\t\t\t    OR (\r\n\t\t\t        pb.batch_name ILIKE '%' || @keyword || '%'\r\n\t\t\t        OR w_to.name ILIKE '%' || @keyword || '%'\r\n\t\t\t    )\r\n        )\r\n        AND (@batch_ids IS NULL OR pb.batch_id = ANY(@batch_ids::uuid[]))\r\n        AND (@product_ids IS NULL OR pb.crm_srv_product_id = ANY(@product_ids::uuid[]))\r\n        AND (@warehouse_to_ids IS NULL OR pb.crm_srv_warehouse_id_to = ANY(@warehouse_to_ids::uuid[]))\r\n        AND (@start_date IS NULL OR pb.transit_date >= @start_date::timestamptz)\r\n        AND (@end_date IS NULL OR pb.transit_date <= @end_date::timestamptz)\r\n),\r\ncounted AS (\r\n    SELECT COUNT(*) AS total_row\r\n    FROM filtered\r\n)\r\nSELECT\r\n    f.*,\r\n    c.total_row\r\nFROM\r\n    filtered f\r\n    CROSS JOIN counted c\r\nORDER BY\r\n    f.transit_date DESC,\r\n    f.created_at DESC\r\nLIMIT @page_size\r\nOFFSET (@page_index * @page_size)	1	2026-02-03 10:46:19.20093+07	HCare.CrmService.WarehouseService	3
61ea9946-695c-464b-8c5c-9e36201e1bc7	dealer_account_mapping_get_by_accounts	dealer_account_mapping_get_by_accounts	\N	SELECT e.*\r\nFROM crm_srv_dealer_account_mapping e\r\nJOIN (\r\n    SELECT unnest(@account_ids::uuid[]) AS account_id\r\n) c\r\n    ON e.acc_srv_account_id = c.account_id\r\nWHERE 0=0	1	2026-01-06 03:06:35.567868+07	HCare.CrmService.DealerService	3
6d3f46eb-599d-43b2-98d8-8b0da07bcb4c	dealer_account_mapping_get_by_dealers	dealer_account_mapping_get_by_dealers	\N	SELECT e.*\r\nFROM crm_srv_dealer_account_mapping e\r\nJOIN (\r\n    SELECT unnest(@dealer_ids::uuid[]) AS dealer_id\r\n) c\r\n    ON e.prd_srv_dealer_id = c.dealer_id\r\nWHERE 0=0	1	2026-01-06 03:07:17.349092+07	HCare.CrmService.DealerService	3
f022068c-5fe0-4315-873d-e247e72010b8	warehouse_get_by_ids	warehouse_get_by_ids	\N	select e.*\r\nFROM crm_srv_warehouse e\r\n         JOIN (\r\n    SELECT unnest(@warehouse_ids::uuid[]) AS id\r\n) c\r\n              ON e.id = c.id\r\nwhere 0 = 0	1	2026-01-06 03:07:17.349092+07	HCare.CrmService.WarehouseService	3
5590e7ef-755c-461b-b0f9-c7cbe45a6e14	unit_get_by_ids	unit_get_by_ids	\N	SELECT e.*\r\nFROM crm_srv_unit e\r\nJOIN (\r\n    SELECT unnest(@unit_ids::uuid[]) AS unit_id\r\n) c\r\n    ON e.id = c.unit_id\r\nWHERE 0=0	1	2026-01-07 07:57:23.04075+07	HCare.CrmService.UnitService	3
f5651437-15b7-419d-b3d5-5ea1b3f29126	product_journey_get_last_by_product_id_and_serial_number	product_journey_get_last_by_product_id_and_serial_number	\N	SELECT *\r\nFROM public.crm_srv_produced_journey\r\n\twhere crm_srv_product_id = @product_id and serial_number = @serial_number\r\nORDER BY id DESC, journey_date DESC\r\nLIMIT 1	1	2026-01-07 05:04:44.603275+07	HCare.CrmService.ProductJourneyService	3
fb626783-7e46-48fa-971d-c5e70d86ca0a	menu_check_existed_title_key	menu_check_existed_title_key	\N	SELECT EXISTS (\r\n    SELECT 1\r\n    FROM acc_srv_account_menu\r\n    WHERE title_key = @title_key\r\n      AND (@id IS NULL OR id <> @id)\r\n) AS is_existed	1	2026-01-06 10:46:06.774613+07	HCare.AccountService.AccountMenu	3
9bdb7431-3b20-4628-8d85-c9dcc0e1216b	account_mapping_get_by_account_id	lấy danh sách account mapping theo account_id	\N	SELECT \r\n\tm.acc_srv_account_id,\r\n\tm.parent_acc_srv_account_id,\r\n\tm.acc_srv_account_group_id,\r\n\tpa.full_name,\r\n    pa.username\r\n    \r\nFROM acc_srv_account_mapping m\r\nINNER JOIN acc_srv_account pa ON m.parent_acc_srv_account_id = pa.id\r\nWHERE m.acc_srv_account_id = @account_id;	1	2026-02-02 06:36:17.797463+07	AccountService.AccountPermissionService	3
441dcd25-b673-42db-b06d-dc435b7b0de1	ward_get_by_id	ward_get_by_id	\N	select * from mlg_srv_ward msw\r\nwhere msw.id = @ward_id	1	2026-01-07 15:49:15.170293+07	HCare.MultilingualismService.LocationService	3
047a580f-2df8-4445-9198-5155af94bf2c	warehouse_product_mapping_get_by_warehouse_id	warehouse_product_mapping_get_by_warehouse_id	\N	select * from crm_srv_warehouse_product_mapping where crm_srv_warehouse_id = @warehouse_id	1	2026-01-08 08:31:55.247721+07	HCare.CrmService.WarehouseService	3
7ab10274-6bc1-4eab-bc15-e3031e46c095	locale_get_by_lang_code_and_resource_module	locale_get_by_lang_code_and_resource_module	\N	SELECT * FROM mlg_srv_locale WHERE mlg_srv_country_lang_code = @lang_code AND (@resource_module IS NULL OR resource_module = @resource_module)	1	2026-01-08 09:11:52.786069+07	HCare.MultilingualismService.LocaleService	3
8593f082-a2fb-4b68-97e4-5a194d81c1d3	product_journey_get_last_by_product_ids_and_serial_numbers	product_journey_get_last_by_product_ids_and_serial_numbers	\N	SELECT DISTINCT ON (j.serial_number) j.*\r\nFROM public.crm_srv_produced_journey j\r\nWHERE j.crm_srv_product_id = ANY(@product_ids::uuid[])\r\n  AND j.serial_number = ANY(@serial_numbers::varchar[])\r\nORDER BY j.serial_number, j.journey_date DESC	1	2026-01-07 06:02:06.520105+07	HCare.CrmService.ProductJourneyService	3
c5903fd2-e12d-49b0-97e7-0e942498b03a	product_journey_get_last_by_product_ids_or_serial_numbers	product_journey_get_last_by_product_ids_or_serial_numbers	\N	SELECT DISTINCT ON (j.serial_number) j.*\r\nFROM public.crm_srv_produced_journey j\r\nWHERE j.crm_srv_product_id = ANY(@product_ids::uuid[])\r\n   OR j.serial_number = ANY(@serial_numbers::varchar[])\r\nORDER BY j.serial_number, j.journey_date DESC	1	2026-01-08 02:12:36.188206+07	HCare.CrmService.ProductJourneyService	3
49564f75-e92b-44c7-a9b7-09f38023700c	product_price_get_by_ids	product_price_get_by_ids	\N	select *\r\nfrom crm_srv_product_price crpp\r\nwhere crpp.id = any(@ids::uuid[])	1	2026-01-09 03:38:59.950972+07	HCare.CrmService.ProductPriceService	3
5cd6c3de-7f4d-4ce3-88a5-b61fa0d99df6	product_price_search	product_price_search	\N	select *\r\nfrom crm_srv_product_price crpp\r\nwhere (@dealer_id is null or crpp.crm_srv_dealer_id = @dealer_id)\r\n  and (@dealer_level_id is null or crpp.crm_srv_dealer_level_id = @dealer_level_id)\r\n  and (@product_ids is null\r\n    or crpp.crm_srv_product_id = any (@product_ids::uuid[]))\r\n  and (@apply_type is null or crpp.apply_type = @apply_type)\r\n  and (\r\n    @start_price is null\r\n        or @end_price is null\r\n        or crpp.price between @start_price and @end_price\r\n    )\r\n  and (@start_date is null or crpp.end_date >= @start_date)\r\n  and (@end_date is null or crpp.start_date <= @end_date);\r\n	1	2026-01-09 03:20:02.510307+07	HCare.CrmService.ProductPriceService	3
38e4d433-92da-4b5d-a5b1-a3bb0e5174bc	unit_search	unit_search	\N	WITH filtered AS (\r\n    SELECT *\r\n    FROM crm_srv_unit u\r\n    WHERE (\r\n            COALESCE(NULLIF(@unit_group, 0), NULL) IS NULL\r\n            OR u.unit_group = @unit_group\r\n          )\r\n      AND (\r\n            COALESCE(NULLIF(@keyword, ''), NULL) IS NULL\r\n            OR u.unit_name ILIKE '%' || @keyword || '%'\r\n            OR u.unit_name_ascii ILIKE '%' || @keyword || '%'\r\n            OR u.description ILIKE '%' || @keyword || '%'\r\n          )\r\n      and is_active = true \r\n)\r\nSELECT f.*, t.total_row\r\nFROM filtered f\r\nCROSS JOIN (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n) t\r\nORDER BY f.created_at DESC\r\nLIMIT @page_size\r\nOFFSET @page_index * @page_size	1	2026-01-06 08:13:26.928095+07	HCare.CrmService.UnitService	3
042cb5a7-8c2a-4241-92ce-638ecf30fca8	product_search	product_search	\N	WITH filtered AS (\r\n    SELECT DISTINCT p.*\r\n    FROM crm_srv_product p\r\n    WHERE (\r\n            @category_ids IS NULL\r\n            OR EXISTS (\r\n                SELECT 1\r\n                FROM jsonb_array_elements_text(p.category_ids::jsonb) AS cat\r\n                WHERE cat::uuid = ANY(@category_ids::uuid[])\r\n            )\r\n          )\r\n      AND (\r\n      \t\t@brand_ids is null \r\n      \t\tor p.crm_srv_brand_id = any(@brand_ids::uuid[])\r\n      )\r\n      AND (\r\n            COALESCE(NULLIF(@keyword, ''), NULL) IS NULL\r\n            OR p.name ILIKE '%' || @keyword || '%'\r\n            OR p.code ILIKE '%' || @keyword || '%'\r\n            OR p.description ILIKE '%' || @keyword || '%'\r\n          )\r\n      AND (\r\n            @is_abstract IS NULL\r\n            OR p.is_abstract = @is_abstract\r\n          )\r\n          \r\n      AND (\r\n            (@statuses IS NULL AND p.status = 1)\r\n            OR p.status = ANY(@statuses::int4[])\r\n        )\r\n      AND p.status <> 0\r\n\r\n\r\n)\r\nSELECT f.*, t.total_row\r\nFROM filtered f\r\nCROSS JOIN (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n) t\r\nORDER BY f.created_at DESC, f.status DESC\r\nLIMIT @page_size\r\nOFFSET @page_index * @page_size	1	2026-01-05 02:53:22.011301+07	HCare.CrmService.ProductService	3
b35a6088-3926-4306-9009-8615d7efdb32	category_get_by_ids	category_get_by_ids	\N	SELECT e.*\r\nFROM crm_srv_category e\r\nJOIN (\r\n    SELECT unnest(@category_ids::uuid[]) AS category_id\r\n) c\r\n    ON e.id = c.category_id\r\nWHERE 0=0\r\norder by created_at desc	1	2026-01-09 16:55:44.548565+07	HCare.CrmService.CategoryService	3
cc31e23a-8d46-48d2-9bfe-a4ae310779ea	manufacture_search	manufacture_search	\N	WITH filtered AS (\r\n    SELECT *\r\n    FROM crm_srv_manufacturer m\r\n    WHERE m.status <> 0\r\n      AND (\r\n            @keyword IS NULL\r\n            OR m."name" ILIKE '%' || @keyword || '%'\r\n            OR m.code ILIKE '%' || @keyword || '%'\r\n            OR m.description ILIKE '%' || @keyword || '%'\r\n            OR m.website ILIKE '%' || @keyword || '%'\r\n      )\r\n      and (\r\n      \t\t@country_ids is null \r\n      \t\tor m.mlg_srv_country_id = any(@country_ids::uuid[])\r\n      )\r\n      AND (\r\n            @status IS NULL\r\n            OR m.status = @status\r\n      )\r\n)\r\nSELECT f.*, t.total_row\r\nFROM filtered f\r\nCROSS JOIN (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n) t\r\nORDER BY f.created_at DESC\r\nOFFSET @page_index * @page_size\r\nLIMIT @page_size	1	2026-01-21 14:01:00.281795+07	Hcare.CrmService.ManufactureService	3
a32589fc-1f0a-4b7d-bfbc-e872fa3babce	category_search	category_search	\N	WITH filtered AS (\r\n    SELECT *\r\n    FROM crm_srv_category p\r\n    WHERE (\r\n            COALESCE(NULLIF(@parent_category_id, ''), NULL) IS NULL\r\n            OR p.parent_category_id = @parent_category_id \r\n      )\r\n      AND (\r\n            COALESCE(NULLIF(@keyword, ''), NULL) IS NULL\r\n            OR p.category_name ILIKE '%' || @keyword || '%'\r\n            OR p.category_name_ascii ILIKE '%' || @keyword || '%'\r\n            OR p.description ILIKE '%' || @keyword || '%'\r\n      )\r\n      AND (\r\n            COALESCE(@is_only_show_base_category, false) = false\r\n            OR p.parent_category_id IS null OR p.parent_category_id = ''\r\n      )\r\n      AND (\r\n\t\t    COALESCE(@is_only_show_variant_category, false) = false\r\n\t\t    OR (p.parent_category_id IS NOT NULL AND p.parent_category_id <> '')\r\n\t  )\r\n      AND p.is_active = true\r\n)\r\nSELECT f.*, t.total_row\r\nFROM filtered f\r\nCROSS JOIN (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n) t\r\nORDER BY f.created_at DESC\r\nOFFSET COALESCE(@page_index,0) * COALESCE(NULLIF(@page_size,0),0)\r\nLIMIT NULLIF(@page_size,0)	1	2026-01-09 17:42:04.379833+07	HCare.CrmService.CategoryService	3
ed9825e9-2178-46cc-adc1-251c6e9db188	account_group_search	account_group_search	\N	WITH filtered AS (\r\n    SELECT ag.*\r\n    FROM acc_srv_account_group ag\r\n    JOIN acc_srv_account a\r\n      ON ag.acc_srv_account_leader_id = a.id\r\n     AND a.status = 1\r\n    JOIN acc_srv_account_info ai\r\n      ON ai.acc_srv_account_id = a.id\r\n     AND ai.status = 1\r\n    WHERE ag.acc_srv_account_member = '00000000-0000-0000-0000-000000000000'::uuid\r\n      AND (\r\n           @keyword IS NULL \r\n           OR ag.group_name ILIKE '%' || @keyword || '%'\r\n           OR ai.full_name ILIKE '%' || @keyword || '%'\r\n           OR ai.company_name ILIKE '%' || @keyword || '%'\r\n           OR a.phone_number ILIKE '%' || @keyword || '%'\r\n           OR a.email ILIKE '%' || @keyword || '%'\r\n          )\r\n      AND (@lead_ids IS NULL OR ag.acc_srv_account_leader_id = ANY(@lead_ids::uuid[]))\r\n      AND (@group_types IS NULL OR ag.group_type = ANY(@group_types::int4[]))\r\n)\r\nSELECT f.*,\r\n       t.total_row\r\nFROM filtered f\r\nCROSS JOIN (SELECT COUNT(*) AS total_row FROM filtered) t\r\nORDER BY f.created_at\r\nLIMIT @page_size OFFSET @page_index * @page_size	1	2026-01-14 13:45:43.576047+07	HCare.AccountService.AccountGroupService	3
764766da-1dda-4c74-ac3d-68e533fb5b6d	generic_formula_get_by_ids	generic_formula_get_by_ids	\N	SELECT *\r\nFROM generic_formula gf\r\nWHERE gf.id = ANY(@ids::uuid[])	1	2026-02-06 07:08:17.34347+07	Hcare.GeneralService.FormulaService	3
18b2db4a-acc1-41a1-bfd3-afd64fbe380c	manufacture_get_by_ids	manufacture_get_by_ids	\N	SELECT e.*\r\nFROM crm_srv_manufacturer e\r\nJOIN (\r\n    SELECT unnest(@ids::uuid[]) AS id\r\n) c\r\n    ON e.id = c.id\r\nWHERE 0=0	1	2026-01-21 14:03:21.904739+07	Hcare.CrmService.ManufactureService	3
ff722538-53ff-4007-8d7e-16686aca7517	warehouse_product_mapping_get_by_warehouse_ids_and_product_ids	warehouse_product_mapping_get_by_warehouse_ids_and_product_ids	\N	SELECT e.*\r\nFROM crm_srv_warehouse_product_mapping e\r\n         JOIN (\r\n    SELECT unnest(@product_ids::uuid[]) AS product_id\r\n) c\r\n              ON e.crm_srv_product_id = c.product_id\r\n         JOIN (\r\n    SELECT unnest(@warehouse_ids::uuid[]) AS warehouse_id\r\n) w\r\n              ON e.crm_srv_warehouse_id = w.warehouse_id\r\nORDER BY e.created_at, e.crm_srv_product_id DESC	1	2026-01-22 02:35:04.602682+07	HCare.CrmService.WarehouseService	3
f300715d-a2e2-4714-8489-6e0b00f01817	product_price_search_case	product_price_search_case	\N	WITH product_filter AS (\r\n    SELECT unnest(@product_ids::uuid[]) AS product_id\r\n),\r\neligible AS (\r\n    SELECT\r\n        p.*,\r\n        CASE\r\n            WHEN @dealer_id IS NOT NULL AND p.crm_srv_dealer_id = @dealer_id THEN 1\r\n            WHEN @dealer_level_id IS NOT NULL AND p.crm_srv_dealer_level_id = @dealer_level_id THEN 2\r\n            WHEN @apply_type IS NOT NULL AND p.apply_type = @apply_type THEN 3\r\n\r\n            WHEN @dealer_id IS NULL\r\n                 AND @dealer_level_id IS NULL\r\n                 AND @apply_type IS NULL\r\n                 AND p.apply_type = 1 THEN 4\r\n\r\n            ELSE 999999\r\n        END AS priority_rank\r\n    FROM crm_srv_product_price p\r\n    JOIN product_filter f\r\n      ON p.crm_srv_product_id = f.product_id\r\n    WHERE p.price IS NOT NULL\r\n)\r\nSELECT DISTINCT ON (crm_srv_product_id) *\r\nFROM eligible\r\nWHERE priority_rank < 999999\r\nORDER BY crm_srv_product_id, priority_rank, created_at DESC	1	2026-01-10 12:44:00.768352+07	HCare.CrmService.ProductPriceService	3
28d85abe-c35d-4d14-bfed-1363e5470109	formula_config_get_by_ids	formula_config_get_by_ids	\N	SELECT *\r\nFROM formula_config gf\r\nWHERE gf.id = ANY(@ids::uuid[])	1	2026-02-09 03:04:21.484572+07	Hcare.GeneralService.FormulaService	3
1c5cb030-e3c7-45bc-a3be-2dcc8207cc1d	product_batch_get_by_ids	product_batch_get_by_ids	\N	select e.*\r\nFROM crm_srv_product_batch e\r\nJOIN (\r\n    SELECT unnest(@product_batch_ids::uuid[]) AS product_batch_id\r\n) c\r\n    ON e.id = c.product_batch_id\r\nwhere 0 = 0	1	2026-01-13 04:40:06.144702+07	Hcare.CrmService.ProductBatchService	3
445580ba-2b74-4cbe-9036-0cc26297d7dc	product_batch_get_last_by_batch_ids	product_batch_get_last_by_batch_ids	\N	SELECT DISTINCT ON (batch_id, crm_srv_product_id) *\r\n\tFROM crm_srv_product_batch\r\nWHERE batch_id = ANY(@batch_ids::uuid[])\r\nORDER BY batch_id, crm_srv_product_id, created_at DESC	1	2026-01-13 06:43:07.689771+07	Hcare.CrmService.ProductBatchService	3
56286a71-7293-4791-b754-e29ee1caf10d	product_batch_get_last_by_batch_id_and_product_ids	product_batch_get_last_by_batch_id_and_product_ids	\N	SELECT DISTINCT ON (batch_id, crm_srv_product_id) *\r\n\tFROM crm_srv_product_batch\r\nWHERE batch_id = @batch_id\r\n\tand crm_srv_product_id = ANY(@product_ids::uuid[])\r\nORDER BY batch_id, crm_srv_product_id, created_at DESC	1	2026-01-14 05:10:22.461654+07	Hcare.CrmService.ProductBatchService	3
5b9b8b25-291f-4e63-b41c-741358853220	warehouse_product_mapping_get_by_warehouse_id_product_ids	warehouse_product_mapping_get_by_warehouse_id_product_ids	\N	SELECT e.*\r\nFROM crm_srv_warehouse_product_mapping e\r\nJOIN (\r\n    SELECT unnest(@product_ids::uuid[]) AS product_id\r\n) c\r\n    ON e.crm_srv_product_id = c.product_id\r\nWHERE e.crm_srv_warehouse_id = @warehouse_id\r\norder by e.created_at, e.crm_srv_product_id desc	1	2026-01-13 16:00:54.090165+07	HCare.CrmService.WarehouseService	3
56badf92-c3e2-4ecf-91af-337d8af5f594	account_group_permission_controller_get_available_codes_by_account_group_id	Lấy danh sách quyền chưa được add vào nhóm theo groupId	\N	WITH filtered_permissions AS (\r\n    SELECT \r\n        p.code,\r\n        p.description,\r\n        p.required_permission,\r\n        p."permission",\r\n        p.version_id,\r\n        p.created_at\r\n    FROM public.acc_srv_account_permission_controller p\r\n    WHERE NOT EXISTS (\r\n        SELECT 1 \r\n        FROM public.acc_srv_account_group_permission_controller gp\r\n        WHERE gp.acc_srv_account_permission_controller_code = p.code\r\n        AND gp.acc_srv_account_group_id = @account_group_id\r\n    )\r\n    AND (\r\n        @keyword IS NULL \r\n        OR p.code ILIKE '%' || @keyword || '%' \r\n        OR p.description ILIKE '%' || @keyword || '%'\r\n    )\r\n)\r\nSELECT \r\n    f.*,\r\n    t.total_row\r\nFROM filtered_permissions f\r\nCROSS JOIN (\r\n    SELECT COUNT(*) AS total_row FROM filtered_permissions\r\n) t\r\nORDER BY f.created_at DESC\r\nLIMIT @page_size OFFSET (@page_index * @page_size);	1	2026-02-02 09:00:03.320339+07	AccountService.AccountPermissionService	3
df987fa8-0ce2-4c60-86fa-46f34d69953f	account_menu_search	account_menu_search	\N	WITH recursive\r\nconstants AS (\r\n    SELECT '00000000-0000-0000-0000-000000000000'::uuid AS empty_guid\r\n),\r\ntarget_nodes AS (\r\n    SELECT id, parent_id FROM acc_srv_account_menu\r\n    WHERE (title_key ILIKE '%' || @keyword || '%' OR @keyword IS NULL)\r\n      AND menu_status <> -1\r\n),\r\nancestors AS (\r\n    SELECT id, parent_id FROM target_nodes\r\n    UNION\r\n    SELECT m.id, m.parent_id\r\n    FROM acc_srv_account_menu m\r\n    INNER JOIN ancestors a ON m.id = a.parent_id\r\n    WHERE m.menu_status <> -1\r\n),\r\ndescendants AS (\r\n    SELECT id, parent_id FROM target_nodes\r\n    UNION\r\n    SELECT m.id, m.parent_id\r\n    FROM acc_srv_account_menu m\r\n    INNER JOIN descendants d \r\n    ON (m.parent_id = d.id OR (m.parent_id = (SELECT empty_guid FROM constants) AND d.id IS NULL))\r\n    WHERE m.menu_status <> -1\r\n),\r\nall_related_ids AS (\r\n    SELECT id FROM ancestors\r\n    UNION\r\n    SELECT id FROM descendants\r\n),\r\nmenu_tree AS (\r\n    SELECT \r\n        m.*, \r\n        ARRAY[ROW(m.display_order, m.id)::text] AS sort_path\r\n    FROM acc_srv_account_menu m\r\n    WHERE m.id IN (SELECT id FROM all_related_ids)\r\n      AND (m.parent_id IS NULL OR m.parent_id = (SELECT empty_guid FROM constants))\r\n      AND m.menu_status <> -1\r\n    UNION ALL\r\n\r\n    SELECT \r\n        child.*, \r\n        parent.sort_path || ROW(child.display_order, child.id)::text\r\n    FROM acc_srv_account_menu child\r\n    INNER JOIN menu_tree parent ON child.parent_id = parent.id\r\n    WHERE child.id IN (SELECT id FROM all_related_ids)\r\n      AND child.menu_status <> -1\r\n)\r\nSELECT * FROM menu_tree\r\nORDER BY sort_path ASC;	1	2025-12-30 07:20:49.47036+07	HCare.AccountService.AccountMenu	3
128998eb-002b-4213-88c6-e5f3072be803	formula_config_get_by_codes	formula_config_get_by_codes	\N	SELECT *\r\nFROM formula_config gf\r\nWHERE gf.code = ANY(@codes::varchar[])	1	2026-02-09 03:34:45.215443+07	Hcare.GeneralService.FormulaService	3
c73bf55f-7413-4d4b-aa0a-87d0d4e5ba26	ward_get_by_ids	ward_get_by_ids	\N	SELECT e.*,\r\n       msd.district_name,\r\n       msp.province_name,\r\n       msc.id as country_id,\r\n       msc.country_name\r\nFROM mlg_srv_ward e\r\n         JOIN (SELECT unnest(@ward_ids::uuid[]) AS ward_id) c\r\n              ON e.id = c.ward_id\r\n         left join public.mlg_srv_district msd on e.mlg_srv_district_id = msd.id\r\n         left join mlg_srv_province msp on e.mlg_srv_province_id = msp.id\r\n         left join mlg_srv_country msc on msp.mlg_srv_country_id = msc.id\r\nWHERE 0 = 0	1	2026-01-12 23:03:37.860964+07	HCare.MultilingualismService.LocationService	3
6248297f-9310-421a-adb4-6960d1ad6c03	province_get_by_ids	province_get_by_ids	\N	SELECT e.*,\r\n       msc.country_name\r\nFROM mlg_srv_province e\r\n         JOIN (SELECT unnest(@province_ids::uuid[]) AS province_id) c\r\n              ON e.id = c.province_id\r\n         left join mlg_srv_country msc on e.mlg_srv_country_id = msc.id\r\nWHERE 0 = 0	1	2026-01-12 22:50:09.405933+07	HCare.MultilingualismService.LocationService	3
f2499199-ebf8-4002-8095-c4ff86c1f50a	account_group_get_account_group_by_account_id	lấy danh sách nhóm quyền của 1 tài khoản	\N	SELECT\r\n asag.id,\r\n asag.acc_srv_account_leader_id,\r\n asag.group_name\r\nFROM acc_srv_account_group AS asag\r\nWHERE\r\n asag.acc_srv_account_member = @account_id;	1	2026-02-09 04:15:24.744182+07	Hcare.AccountService.AccountGroupService	3
a5321464-9a97-4851-86a4-d9d3b44e34ea	countries_search	countries_search	\N	\r\nWITH filtered AS (\r\n    SELECT *\r\n    FROM public.mlg_srv_country\r\n    WHERE (@keyword IS NULL\r\n              OR country_name_ascii ILIKE '%' || @keyword || '%'\r\n              OR country_code ILIKE '%' || @keyword || '%'\r\n              OR default_lang ILIKE '%' || @keyword || '%')\r\n      AND status = 1\r\n      AND (@is_enable_translate IS NULL OR is_enable_lang_code = @is_enable_translate::bool)\r\n)\r\nSELECT f.*, t.total_row\r\nFROM filtered f\r\nCROSS JOIN (SELECT COUNT(*) AS total_row FROM filtered) t\r\nORDER BY is_enable_lang_code DESC, country_name_ascii\r\nLIMIT @page_size OFFSET @page_index * @page_size\r\n	1	2025-12-17 09:19:39.931187+07	HCare.MultilingualismService.LocationService	3
70e08bdf-dddd-4d9b-b3bc-791ccf1b1af9	product_warranty_search	product_warranty_search	\N	select w.*,\r\n       count(*) over () as total_row\r\nfrom crm_srv_product_warranty w\r\nwhere w.is_active = true\r\n  and (\r\n    nullif(@keyword, '') is null\r\n        or @keyword = w.serial_number\r\n        or @keyword = w.phone_number\r\n    )\r\nlimit @page_size offset @page_index * @page_size;\r\n	1	2026-02-09 04:53:49.334696+07	Hcare.CrmService.ProductWarrantyService	3
ad782489-620c-4198-8a49-f4eb5a780f7f	province_search	province_search	\N	WITH filtered_province AS (\r\n    SELECT\r\n        pr.*,\r\n        msc.country_name\r\n    FROM mlg_srv_province pr\r\n    LEFT JOIN mlg_srv_country msc\r\n        ON msc.id = pr.mlg_srv_country_id\r\n    WHERE pr.status = 1\r\n\r\n      -- keyword search\r\n      AND (\r\n          NULLIF(@keyword, '') IS NULL\r\n          OR (\r\n              pr.id::text = @keyword\r\n              OR pr.province_name ILIKE '%' || @keyword || '%'\r\n              OR pr.province_code ILIKE '%' || @keyword || '%'\r\n          )\r\n      )\r\n\r\n      AND (NULLIF(@province_name, '') IS NULL\r\n           OR pr.province_name ILIKE '%' || @province_name || '%')\r\n      AND (NULLIF(@province_code, '') IS NULL\r\n           OR pr.province_code ILIKE '%' || @province_code || '%')\r\n      AND (NULLIF(@province_rcd, '') IS NULL\r\n           OR pr.province_rcd = @province_rcd)\r\n      AND (@country_id IS NULL\r\n           OR pr.mlg_srv_country_id = @country_id)\r\n),\r\n\r\ncount_total AS (\r\n    SELECT COUNT(1) AS total_row\r\n    FROM filtered_province\r\n)\r\n\r\nSELECT\r\n    fp.*,\r\n    ct.total_row\r\nFROM filtered_province fp\r\nCROSS JOIN count_total ct\r\nORDER BY fp.created_at DESC\r\nLIMIT @page_size\r\nOFFSET @page_index * @page_size;\r\n	1	2025-12-22 12:03:24.831668+07	HCare.MultilingualismService.LocationService	3
03cb9ef8-ce20-41ef-9226-9e52af3bb0cd	product_warranty_get_by_ids	product_warranty_get_by_ids	\N	select w.*\r\nfrom crm_srv_product_warranty w\r\nwhere w.is_active = true\r\n  and w.id = any(@ids::uuid[])\r\n	1	2026-02-09 04:56:19.501457+07	Hcare.CrmService.ProductWarrantyService	3
c3a85536-6790-498a-8fb7-a0d8542649df	account_group_get_member_in_group_id	account_group_get_member_in_group_id	\N	WITH filtered AS (\r\n    SELECT ai.*\r\n    FROM acc_srv_account_group ag\r\n    JOIN acc_srv_account_info ai\r\n    \tON ag.acc_srv_account_member = ai.acc_srv_account_id and ai.status = 1\r\n    JOIN acc_srv_account a\r\n      \tON ai.acc_srv_account_id = a.id and a.status = 1\r\n    WHERE ag.id = @group_id::uuid\r\n      AND (@keyword IS NULL \r\n      \t   OR ag.group_name LIKE '%' || @keyword || '%'\r\n           OR ai.full_name LIKE '%' || @keyword || '%'\r\n           OR a.phone_number LIKE '%' || @keyword || '%'\r\n           OR a.email LIKE '%' || @keyword || '%'\r\n           )\r\n      AND (@permissions IS NULL OR a.permission = ANY(@permissions::int4[]))\r\n)\r\nSELECT f.*,\r\n       t.total_row\r\nFROM filtered f\r\nCROSS JOIN (SELECT COUNT(*) AS total_row FROM filtered) t\r\nORDER BY f.full_name\r\nLIMIT @page_size OFFSET @page_index * @page_size	1	2026-01-14 12:41:40.340707+07	HCare.AccountService.AccountGroupService	3
75821f79-eb21-4dfa-afb1-0472ce72ef75	account_dashboard_view_get_by_permissions_or_account_id	account_dashboard_view_get_by_permissions_or_account_id	\N	\r\nWITH filtered AS (\r\n    SELECT v.*\r\n    FROM public.vw_account_permission_extended_stats v\r\n    WHERE (\r\n        @permissions::int[] IS NULL\r\n        OR EXISTS (\r\n            SELECT 1\r\n            FROM unnest(@permissions::int[]) AS p\r\n            WHERE (v.permission & p) = p\r\n        )\r\n    )\r\n    AND (\r\n        @account_id IS NULL OR v.account_id = @account_id\r\n    )\r\n)\r\nSELECT \r\n    f.*,\r\n    t.total_row\r\nFROM filtered f\r\nCROSS JOIN (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n) t\r\nORDER BY f.priority, f.total_users desc\r\nLIMIT @page_size OFFSET (@page_index * @page_size)	1	2026-02-24 08:48:34.50983+07	HCare.AccountService.AccountDashboardViewService	3
095bbb03-7de3-4f7c-8c91-db1e513cac4f	district_search	district_search	\N	WITH filtered_district AS (\r\n    SELECT\r\n        msd.*,\r\n        msp.province_name,\r\n        msc.id   AS country_id,\r\n        msc.country_name\r\n    FROM mlg_srv_district msd\r\n    INNER JOIN mlg_srv_province msp\r\n        ON msp.id = msd.mlg_srv_province_id\r\n    LEFT JOIN mlg_srv_country msc\r\n        ON msp.mlg_srv_country_id = msc.id\r\n    WHERE msd.status = 1\r\n\r\n      -- keyword search (id + name + code)\r\n      AND (\r\n          NULLIF(@keyword, '') IS NULL\r\n          OR (\r\n              msd.id::text = @keyword\r\n              OR msd.district_name ILIKE '%' || @keyword || '%'\r\n              OR msd.district_code ILIKE '%' || @keyword || '%'\r\n          )\r\n      )\r\n\r\n      AND (NULLIF(@district_name, '') IS NULL\r\n           OR msd.district_name ILIKE '%' || @district_name || '%')\r\n      AND (NULLIF(@district_code, '') IS NULL\r\n           OR msd.district_code = @district_code)\r\n      AND (@province_id IS NULL\r\n           OR msd.mlg_srv_province_id = @province_id)\r\n      AND (@country_id IS NULL\r\n           OR msc.id = @country_id)\r\n),\r\n\r\ncount_total AS (\r\n    SELECT COUNT(1) AS total_row\r\n    FROM filtered_district\r\n)\r\n\r\nSELECT\r\n    fd.*,\r\n    ct.total_row\r\nFROM filtered_district fd\r\nCROSS JOIN count_total ct\r\nORDER BY fd.created_at DESC\r\nLIMIT @page_size\r\nOFFSET @page_size * @page_index;\r\n	1	2026-01-15 02:22:42.700142+07	HCare.MultilingualismService.LocationService	3
9c137cec-6418-466f-a1c0-c6778b90a7a6	district_get_by_ids	district_get_by_ids	\N	SELECT e.*,\r\n       msp.id AS province_id,\r\n       msp.province_name,\r\n       msc.id AS country_id,\r\n       msc.country_name\r\nFROM mlg_srv_district e\r\nLEFT JOIN mlg_srv_province msp\r\n    ON e.mlg_srv_province_id = msp.id\r\nLEFT JOIN mlg_srv_country msc\r\n    ON msp.mlg_srv_country_id = msc.id\r\nWHERE e.id = ANY(@ids::uuid[]);\r\n	1	2026-01-15 05:48:57.924269+07	HCare.MultilingualismService.LocationService	3
afe59c0f-bcad-4ae0-a22f-7d7018bba5c7	product_attribute_search	product_attribute_search	\N	WITH filtered AS (\r\n    SELECT a.* \r\n    FROM crm_srv_product_attribute a\r\n    JOIN crm_srv_product p ON a.crm_srv_product_id = p.id\r\n--    LEFT JOIN crm_srv_product_attribute_value v ON a.id = v.crm_srv_product_attribute_id\r\n    WHERE \r\n    \tp.status <> 0 and a.status <> 0 \r\n      -- keyword filter (search in attribute_name, attribute_code, description)\r\n      AND (@keyword IS NULL OR (\r\n            a.attribute_name ILIKE '%' || @keyword || '%' OR\r\n            a.attribute_code ILIKE '%' || @keyword || '%' OR\r\n            a.description ILIKE '%' || @keyword || '%'\r\n      ))\r\n      -- product_name filter\r\n      AND (@product_name IS NULL OR p."name" ILIKE '%' || @product_name || '%')\r\n      -- product_ids filter\r\n      AND (\r\n        @product_ids IS NULL\r\n        OR p.id IN (SELECT unnest(@product_ids::uuid[]))\r\n      )\r\n      -- product_parent_ids filter\r\n      AND (\r\n        @product_parent_ids IS NULL\r\n        OR p.parent_id IN (SELECT unnest(@product_parent_ids::uuid[]))\r\n      )\r\n      -- data_types filter\r\n      AND (\r\n        @data_types IS NULL\r\n        OR a.data_type IN (SELECT unnest(@data_types::int4[]))\r\n      )\r\n      -- select_types filter\r\n      AND (\r\n        @select_types IS NULL\r\n        OR a.select_type IN (SELECT unnest(@select_types::int4[]))\r\n      )\r\n      -- is_abstract_product filter\r\n      AND (\r\n        @is_abstract_product IS NULL\r\n        OR p.is_abstract = (@is_abstract_product::bool)\r\n      )\r\n)\r\n, counted AS (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n)\r\nSELECT f.*, c.total_row\r\nFROM filtered f\r\nCROSS JOIN counted c\r\nORDER BY f.created_at DESC\r\nLIMIT @page_size\r\nOFFSET (@page_index * @page_size)	1	2026-01-19 08:49:01.129256+07	HCare.CrmService.ProductService	3
448d7ff1-4b06-42ce-8e9c-f3d25a43466a	account_group_permission_controller_search_by_account_group_id	lấy danh sách quyền của group	\N	WITH filtered_permissions AS (\r\n    SELECT\r\n        gp.acc_srv_account_group_id,\r\n        gp.acc_srv_account_permission_controller_code,\r\n        p.description,\r\n        p."permission",\r\n        p.required_permission,\r\n        p.version_id,\r\n        gp.created_at\r\n    FROM public.acc_srv_account_group_permission_controller gp\r\n    JOIN public.acc_srv_account_permission_controller p\r\n        ON gp.acc_srv_account_permission_controller_code = p.code\r\n    WHERE (\r\n        @keyword IS NULL\r\n        OR gp.acc_srv_account_permission_controller_code ILIKE '%' || @keyword || '%' \r\n        OR p.description ILIKE '%' || @keyword || '%'\r\n    )\r\n    AND gp.acc_srv_account_group_id = @account_group_id\r\n)\r\nSELECT \r\n    *,\r\n    COUNT(*) OVER() AS total_row\r\nFROM filtered_permissions\r\nORDER BY created_at DESC\r\nLIMIT @page_size \r\nOFFSET (@page_index * @page_size);	1	2026-02-03 06:44:42.140466+07	AccountService.AccountPermissionControllerService	3
279d35a4-cb1a-4181-a5b9-420c7a41f5e6	get_addresses_of_multiple_account_ids	get_addresses_of_multiple_account_ids	\N	select * from acc_srv_account_address asaa\r\nwhere asaa.acc_srv_account_id = any(@account_ids::uuid[])	1	2026-01-20 06:36:36.130361+07	HCare.AccountService.AccountInfoService	3
61dcdcca-24b0-45b9-bab9-6247ec7bf17a	warehouse_get_list_product_by_warehouse_id	warehouse_get_list_product_by_warehouse_id	\N	SELECT\r\n    w.id AS warehouse_id,\r\n    w.name AS warehouse_name,\r\n    w.code AS warehouse_code,\r\n    w.address AS warehouse_address,\r\n    w.status AS warehouse_status,\r\n    p.id AS product_id,\r\n    p.name AS product_name,\r\n    p.code AS product_code,\r\n    p.description AS product_description,\r\n    wpm.quantity AS total_quantity,\r\n    wpm.available_quantity AS available_qty,\r\n    wpm.last_import_date,\r\n    wpm.last_export_date,\r\n    wpm.is_active AS mapping_is_active\r\nFROM crm_srv_warehouse w\r\n         LEFT JOIN crm_srv_warehouse_product_mapping wpm ON w.id = wpm.crm_srv_warehouse_id\r\n         LEFT JOIN crm_srv_product p ON wpm.crm_srv_product_id = p.id\r\nWHERE w.id = @warehouse_id;	1	2026-01-15 06:48:15.166518+07	HCare.CrmService.WarehouseService	3
bddea19d-6782-45f5-9ed5-2b2bcd81f5de	product_batch_search	product_batch_search	\N	WITH FilteredBatches AS (\r\n        SELECT DISTINCT \r\n            batch_id, \r\n            MAX(created_at) as last_created\r\n        FROM \r\n            crm_srv_product_batch\r\n        WHERE \r\n            (@keyword IS NULL OR batch_name LIKE '%' || @keyword || '%')\r\n        GROUP BY\r\n            batch_id\r\n    ),\r\n    PaginatedBatches AS (\r\n        SELECT batch_id\r\n        FROM FilteredBatches \r\n        ORDER BY last_created DESC\r\n        LIMIT @page_size\r\n        OFFSET @page_index * @page_size\r\n    )\r\n    SELECT pb.*,\r\n        (SELECT COUNT(*) FROM FilteredBatches) as total_row\r\n    FROM crm_srv_product_batch pb\r\n    JOIN PaginatedBatches p ON pb.batch_id = p.batch_id\r\n    join crm_srv_product csp on pb.crm_srv_product_id = csp.id and csp.status = 1\r\n    \tand (@product_name IS NULL OR csp.name LIKE '%' || @product_name || '%')\r\n      \tAND (@product_ids IS NULL OR csp.id = ANY(@product_ids::uuid[]))\r\n    join crm_srv_warehouse csw on pb.crm_srv_warehouse_id_to = csw.id and csw.status = 1\r\n    \tand (@warehouse_name IS NULL OR csw.name LIKE '%' || @warehouse_name || '%')\r\n      \tAND (@warehouse_ids IS NULL OR csw.id = ANY(@warehouse_ids::uuid[]))\r\n    WHERE pb.is_active = true\r\n    ORDER BY pb.created_at desc	1	2026-01-14 12:58:09.851975+07	Hcare.CrmService.ProductBatchService	3
8a8c36bf-6676-4929-b831-132467c7846a	warehouse_product_search	warehouse_product_search	\N	WITH FilteredMappings AS (\r\n    SELECT\r\n        cwpm.id,\r\n        cwpm.created_at\r\n    FROM crm_srv_warehouse_product_mapping cwpm\r\n    JOIN crm_srv_product csp\r\n        ON cwpm.crm_srv_product_id = csp.id\r\n        AND csp.status = 1\r\n    JOIN crm_srv_warehouse csw\r\n        ON cwpm.crm_srv_warehouse_id = csw.id\r\n        AND csw.status = 1\r\n    WHERE\r\n        cwpm.is_active = true\r\n        AND (@product_name IS NULL OR csp.name LIKE '%' || @product_name || '%')\r\n        AND (@product_ids IS NULL OR csp.id = ANY(@product_ids::uuid[]))\r\n        AND (@warehouse_name IS NULL OR csw.name LIKE '%' || @warehouse_name || '%')\r\n        AND (@warehouse_ids IS NULL OR csw.id = ANY(@warehouse_ids::uuid[]))\r\n),\r\nPaginatedMappings AS (\r\n    SELECT id\r\n    FROM FilteredMappings\r\n    ORDER BY created_at DESC\r\n    LIMIT @page_size\r\n    OFFSET @page_index * @page_size\r\n)\r\nSELECT\r\n    cwpm.*,\r\n    (SELECT COUNT(*) FROM FilteredMappings) AS total_row\r\nFROM crm_srv_warehouse_product_mapping cwpm\r\nJOIN PaginatedMappings pm ON cwpm.id = pm.id\r\nORDER BY cwpm.created_at DESC	1	2026-01-15 15:53:11.002084+07	HCare.CrmService.WarehouseService	3
271935ff-127c-4d59-99dc-23b3c4799fe9	district_get_by_id	district_get_by_id	\N	select * from mlg_srv_district where id = @district_id	1	2026-01-15 10:42:11.934625+07	HCare.MultilingualismService.LocationService	3
d34fbf5c-aaef-4cf6-adc5-42d2bfce261f	product_attribute_mapping_get_by_product_ids	product_attribute_mapping_get_by_product_ids	\N	SELECT e.*\r\nFROM crm_srv_product_attribute_mapping e\r\nJOIN (\r\n    SELECT unnest(@product_ids::uuid[]) AS crm_srv_product_id\r\n) c\r\n    ON e.crm_srv_product_id = c.crm_srv_product_id\r\nWHERE 0=0	1	2026-01-15 16:06:25.716209+07	HCare.CrmService.ProductService	3
708e30ca-3024-4886-94ef-4348fe4147ad	category_get_by_parent_ids	category_get_by_parent_ids	\N	SELECT e.*\r\nFROM crm_srv_category e\r\nJOIN (\r\n    SELECT unnest(@parent_category_ids::uuid[]) AS parent_category_id\r\n) c\r\n    ON e.parent_category_id = c.parent_category_id\r\nWHERE 0=0\r\norder by created_at desc	1	2026-01-09 16:55:44.548+07	HCare.CrmService.CategoryService	3
f5f12557-da48-4c9c-97b0-051ffb966e0f	category_find_flatten_tree_from_nodes	category_find_flatten_tree_from_nodes	\N	WITH RECURSIVE search_parent AS (\r\n    -- (Parents)\r\n    SELECT c.*\r\n    FROM crm_srv_category c\r\n    WHERE @is_find_parent = TRUE ------\r\n      AND c.id = ANY(@category_ids::uuid[])\r\n      \r\n    UNION ALL\r\n   \r\n    SELECT p.*\r\n    FROM search_parent a\r\n    JOIN LATERAL (\r\n        SELECT TRIM(BOTH '"' FROM cat)::uuid AS parent_id\r\n        FROM jsonb_array_elements_text(\r\n            CASE\r\n                WHEN a.parent_category_id IS NULL OR a.parent_category_id = '' THEN '[]'::jsonb\r\n                WHEN jsonb_typeof(a.parent_category_id::jsonb) = 'array' THEN a.parent_category_id::jsonb\r\n                ELSE jsonb_build_array(a.parent_category_id)\r\n            END\r\n        ) AS cat\r\n    ) j ON TRUE\r\n    JOIN crm_srv_category p ON p.id = j.parent_id\r\n),\r\nsearch_children AS (\r\n    -- (Children)\r\n    SELECT c.*\r\n    FROM crm_srv_category c\r\n    WHERE @is_find_children = TRUE ------\r\n      AND c.id = ANY(@category_ids::uuid[])\r\n    \r\n    UNION ALL\r\n    \r\n    SELECT child.*\r\n    FROM search_children sc\r\n    JOIN crm_srv_category child\r\n      ON EXISTS (\r\n          SELECT 1\r\n          FROM jsonb_array_elements_text(\r\n              CASE\r\n                  WHEN child.parent_category_id IS NULL OR child.parent_category_id = '' THEN '[]'::jsonb\r\n                  WHEN jsonb_typeof(child.parent_category_id::jsonb) = 'array' THEN child.parent_category_id::jsonb\r\n                  ELSE jsonb_build_array(child.parent_category_id)\r\n              END\r\n          ) AS cat\r\n          WHERE TRIM(BOTH '"' FROM cat)::uuid = sc.id\r\n      )\r\n)\r\nSELECT *\r\nFROM (\r\n    SELECT * FROM search_parent\r\n    UNION\r\n    SELECT * FROM search_children\r\n) result	1	2026-01-10 23:54:14.11216+07	HCare.CrmService.ProductPriceService	3
31ddadc4-f802-4ad2-85ba-802d4a8eca44	order_product_search	order_product_search	\N	SELECT *\r\nFROM crm_srv_order cso\r\nWHERE cso.order_id = ANY(@order_ids::uuid[]);\r\n	1	2026-01-20 15:41:13.059289+07	HCare.CrmService.OrderService	3
07189e6a-e931-4bdf-b4f6-fe102e967b32	brand_get_by_ids	brand_get_by_ids	\N	SELECT e.*\r\nFROM crm_srv_brand e\r\nJOIN (\r\n    SELECT unnest(@ids::uuid[]) AS id\r\n) c\r\n    ON e.id = c.id\r\nWHERE 0=0	1	2026-01-21 07:09:29.010521+07	Hcare.CrmService.BrandService	3
ae03ef79-cd26-4738-ad84-e092af77dccb	brand_search	brand_search	\N	WITH filtered AS (\r\n    SELECT *\r\n    FROM crm_srv_brand b\r\n    WHERE b.status <> 0\r\n      AND (\r\n            @keyword IS NULL\r\n            OR b."name" ILIKE '%' || @keyword || '%'\r\n            OR b.code ILIKE '%' || @keyword || '%'\r\n            OR b.description ILIKE '%' || @keyword || '%'\r\n      )\r\n      AND (\r\n            @status IS NULL\r\n            OR b.status = @status\r\n      )\r\n)\r\nSELECT f.*, t.total_row\r\nFROM filtered f\r\nCROSS JOIN (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n) t\r\nORDER BY f.created_at DESC\r\nOFFSET @page_index * @page_size\r\nLIMIT @page_size	1	2026-01-21 08:53:19.979759+07	Hcare.CrmService.BrandService	3
02a96d95-4315-40ba-9b00-fe5a7b874603	account_group_get_available_members_to_add_group_id	account_group_get_available_members_to_add_group_id	\N	WITH group_info AS (\r\n    SELECT DISTINCT id, acc_srv_account_leader_id, group_name\r\n    FROM acc_srv_account_group\r\n    WHERE id = @group_id::uuid\r\n    LIMIT 1\r\n),\r\nfiltered AS (\r\n    SELECT ai.*\r\n    FROM acc_srv_account a\r\n    JOIN acc_srv_account_info ai\r\n        ON ai.acc_srv_account_id = a.id\r\n    CROSS JOIN group_info ag\r\n    WHERE a.status = 1\r\n      -- loại bỏ leader\r\n      AND a.id <> ag.acc_srv_account_leader_id\r\n      -- loại bỏ những account đã là member trong group\r\n      AND NOT EXISTS (\r\n          SELECT 1\r\n          FROM acc_srv_account_group agm\r\n          WHERE agm.id = ag.id\r\n            AND agm.acc_srv_account_member = a.id\r\n      )\r\n      -- lọc theo keyword\r\n      AND (\r\n           @keyword IS NULL \r\n           OR ag.group_name ILIKE '%' || @keyword || '%'\r\n           OR ai.full_name ILIKE '%' || @keyword || '%'\r\n           OR a.phone_number ILIKE '%' || @keyword || '%'\r\n           OR a.email ILIKE '%' || @keyword || '%'\r\n          )\r\n      -- lọc theo permissions\r\n      AND (@permissions IS NULL OR a.permission = ANY(@permissions::int4[]))\r\n      AND ai.status = 1\r\n)\r\nSELECT f.*,\r\n       t.total_row\r\nFROM filtered f\r\nCROSS JOIN (SELECT COUNT(*) AS total_row FROM filtered) t\r\nORDER BY f.full_name\r\nLIMIT @page_size OFFSET @page_index * @page_size;\r\n	1	2026-01-14 13:17:13.208477+07	HCare.AccountService.AccountGroupService	3
1a0d49a2-9961-4339-8e06-c1eb04f95775	ward_search	ward_search	\N	\r\nWITH filtered_ward AS (\r\n    SELECT\r\n        msw.*,\r\n        msp.province_name,\r\n        msc.id   AS country_id,\r\n        msc.country_name\r\n    FROM mlg_srv_ward msw\r\n    LEFT JOIN mlg_srv_province msp\r\n        ON msp.id = msw.mlg_srv_province_id\r\n    LEFT JOIN mlg_srv_country msc\r\n        ON msc.id = msp.mlg_srv_country_id\r\n    WHERE msw.status = 1\r\n\r\n      -- keyword search (id + name + code)\r\n      AND (\r\n          NULLIF(@keyword, '') IS NULL\r\n          OR (\r\n              msw.id::text = @keyword\r\n              OR msw.ward_name ILIKE '%' || @keyword || '%'\r\n              OR msw.ward_code ILIKE '%' || @keyword || '%'\r\n          )\r\n      )\r\n\r\n      AND (@province_id IS NULL OR msw.mlg_srv_province_id = @province_id)\r\n      AND (NULLIF(@ward_name, '') IS NULL\r\n           OR msw.ward_name ILIKE '%' || @ward_name || '%')\r\n      AND (NULLIF(@ward_code, '') IS NULL\r\n           OR msw.ward_code = @ward_code)\r\n      AND (@country_id IS NULL OR msp.mlg_srv_country_id = @country_id)\r\n),\r\n\r\ncount_total AS (\r\n    SELECT COUNT(1) AS total_row\r\n    FROM filtered_ward\r\n)\r\n\r\nSELECT\r\n    fw.*,\r\n    ct.total_row\r\nFROM filtered_ward fw\r\nCROSS JOIN count_total ct\r\nORDER BY fw.created_at DESC, fw.id DESC\r\nLIMIT @page_size\r\nOFFSET @page_index * @page_size;\r\n\r\n	1	2026-01-07 15:44:10.21704+07	HCare.MultilingualismService.LocationService	3
642f2c28-7f16-4f7b-acb1-1df43a66b036	account_info_search	account_info_search	\N	WITH base AS (\r\n    SELECT ai.id\r\n    FROM public.acc_srv_account_info ai\r\n    INNER JOIN public.acc_srv_account a\r\n        ON a.id = ai.acc_srv_account_id\r\n    LEFT JOIN public.acc_srv_account_address ad\r\n        ON ad.acc_srv_account_id = ai.acc_srv_account_id\r\n    WHERE 1=1\r\n        -- keyword (case-insensitive LIKE)\r\n        AND (\r\n            @keyword::text IS NULL OR (\r\n                a.username      ILIKE '%' || @keyword::text || '%' OR\r\n                a.full_name     ILIKE '%' || @keyword::text || '%' OR\r\n                a.email         ILIKE '%' || @keyword::text || '%' OR\r\n                a.phone_number  ILIKE '%' || @keyword::text || '%'\r\n            )\r\n        )\r\n        -- fullname (info)\r\n        AND (@fullname::text IS NULL OR ai.full_name ILIKE '%' || @fullname::text || '%')\r\n        -- email (account)\r\n        AND (@email::text IS NULL OR a.email ILIKE '%' || @email::text || '%')\r\n        -- phone_number (account)\r\n        AND (@phone_number::text IS NULL OR a.phone_number ILIKE '%' || @phone_number::text || '%')\r\n        -- gender (info)\r\n        AND (@gender::int4 IS NULL OR ai.gender = @gender::int4)\r\n        -- geo filters (address)\r\n        AND (@country_ids::uuid[]  IS NULL OR ad.country_id  IN (SELECT unnest(@country_ids::uuid[])))\r\n        AND (@province_ids::uuid[] IS NULL OR ad.province_id IN (SELECT unnest(@province_ids::uuid[])))\r\n        AND (@district_ids::uuid[] IS NULL OR ad.district_id IN (SELECT unnest(@district_ids::uuid[])))\r\n        AND (@ward_ids::uuid[]     IS NULL OR ad.ward_id     IN (SELECT unnest(@ward_ids::uuid[])))\r\n        -- address_line (address)\r\n        AND (@address_line::text IS NULL OR ad.address_line ILIKE '%' || @address_line::text || '%')\r\n        -- current_lang_code (info)\r\n        AND (@current_lang_code::text IS NULL OR ai.current_lang_code = @current_lang_code::text)\r\n        -- permissions (bitmask)\r\n        AND (\r\n            @permissions::int[] IS NULL OR EXISTS (\r\n                SELECT 1\r\n                FROM unnest(@permissions::int[]) AS p\r\n                WHERE (a.permission & p) = p\r\n            )\r\n        )\r\n        AND a.status <> 0  -- exclude deleted accounts\r\n        -- account_statuses\r\n        AND (@account_statuses::int[] IS NULL OR a.status IN (SELECT unnest(@account_statuses::int[])))\r\n        AND ai.status = 1\r\n),\r\nids AS (\r\n    SELECT DISTINCT b.id\r\n    FROM base b\r\n)\r\nSELECT ai.*, t.total_row\r\nFROM public.acc_srv_account_info ai\r\nJOIN ids i ON i.id = ai.id\r\nCROSS JOIN (SELECT COUNT(*) AS total_row FROM ids) t\r\nORDER BY ai.created_at DESC\r\nLIMIT @page_size::int4 OFFSET (@page_index::int4 * @page_size::int4)	1	2026-01-12 14:53:27.931564+07	HCare.AccountService.AccountInfoService	3
abe25e7e-a90a-4725-97c8-cc146aa3aad2	warehouse_search	warehouse_search	\N	WITH filtered AS (\r\n    SELECT\r\n        d.*,\r\n        COALESCE(wpm.total_products, 0) AS total_products\r\n    FROM crm_srv_warehouse d\r\n             LEFT JOIN (\r\n        SELECT\r\n            cwpm.crm_srv_warehouse_id,\r\n            SUM(COALESCE(cwpm.total_amount_batch, 0)) AS total_products\r\n        FROM crm_srv_warehouse_product_mapping cwpm\r\n                 INNER JOIN crm_srv_product csp\r\n                            ON csp.id = cwpm.crm_srv_product_id\r\n                                AND csp.status = 1\r\n        WHERE cwpm.is_active = true\r\n        GROUP BY cwpm.crm_srv_warehouse_id\r\n    ) wpm\r\n                       ON wpm.crm_srv_warehouse_id = d.id\r\n    WHERE\r\n        (\r\n            @keyword IS NULL\r\n                OR d.name ILIKE '%' || @keyword || '%'\r\n                OR d.code ILIKE '%' || @keyword || '%'\r\n                OR d.address ILIKE '%' || @keyword || '%'\r\n            )\r\n      AND d.status <> 0\r\n      AND (\r\n        @ward_id IS NULL\r\n            OR d.mlg_srv_ward_id = @ward_id\r\n        )\r\n      AND (\r\n        @district_id IS NULL\r\n            OR d.mlg_srv_district_id = @district_id\r\n        )\r\n      AND (\r\n        @province_id IS NULL\r\n            OR d.mlg_srv_province_id = @province_id\r\n        )\r\n      AND (\r\n        @country_id IS NULL\r\n            OR d.mlg_srv_country_id = @country_id\r\n        )\r\n)\r\nSELECT\r\n    f.*,\r\n    t.total_row\r\nFROM filtered f\r\n         CROSS JOIN (\r\n    SELECT COUNT(*) AS total_row\r\n    FROM filtered\r\n) t\r\nORDER BY f.created_at DESC\r\nLIMIT COALESCE(@page_size, 9223372036854775807)\r\n    OFFSET COALESCE(@page_index, 0) * COALESCE(@page_size, 0);\r\n	1	2026-01-23 02:20:32.154161+07	HCare.CrmService.WarehouseService	3
e5733381-6e45-4f10-98c0-680a4c50020e	account_permission_controller_search	danh sách quyền api	\N	WITH filtered AS (\r\n    SELECT *\r\n    FROM acc_srv_account_permission_controller\r\n    WHERE\r\n        (\r\n            @keyword IS NULL\r\n            OR code ILIKE '%' || @keyword || '%'\r\n            OR description ILIKE '%' || @keyword || '%'\r\n        )\r\n)\r\nSELECT \r\n    f.*,\r\n    t.total_row\r\nFROM filtered f\r\nCROSS JOIN (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n) t\r\nORDER BY f.created_at\r\nLIMIT @page_size OFFSET (@page_index * @page_size)	1	2026-01-27 15:54:08.134975+07	AccountService.AccountPermissionService	3
8f374573-5b28-4ba4-a298-be5b44d0cba8	account_search	danh sách tài khoản	\N	WITH filtered AS (\r\n    SELECT a.*\r\n    FROM acc_srv_account a\r\n    WHERE\r\n        (\r\n            @keyword IS NULL\r\n            OR a.username ILIKE '%' || @keyword || '%'\r\n            OR a.full_name ILIKE '%' || @keyword || '%'\r\n        )\r\n    AND (\r\n            @permissions::int[] IS NULL OR EXISTS (\r\n                SELECT 1\r\n                FROM unnest(@permissions::int[]) AS p\r\n                WHERE (a.permission & p) = p\r\n            )\r\n        )\r\n    AND a.status <> 0\r\n)\r\nSELECT \r\n    f.*,\r\n    t.total_row\r\nFROM filtered f\r\nCROSS JOIN (\r\n    SELECT COUNT(*) AS total_row FROM filtered\r\n) t\r\nORDER BY f.created_at\r\nLIMIT @page_size OFFSET (@page_index * @page_size)	1	2026-01-30 02:56:42.824144+07	AccountService.AccountService	3
c02b83c7-019d-48c6-83d5-42f2ba192f2f	dealer_level_get_by_default_account_types	Lay dealer level mac dinh cua cac loai tai khoan	\N	select *\r\nfrom crm_srv_dealer_level\r\nwhere default_account_type = any(@account_types::int4[])	1	2026-02-03 08:40:18.261244+07	HCare.CrmService.DealerService	3
c3e5d906-2bc4-4c5d-b5b1-d19e8979cef9	account_login_info_get_by_last_sync_date	account_login_info_get_by_last_sync_date	\N	SELECT *\r\nFROM public.acc_srv_account_login\r\nORDER BY last_sync_date ASC\r\nLIMIT 1	1	2026-02-05 02:34:25.802646+07	AccountService.AuthenService	3
0a535f72-d6e7-4250-9205-0e9b5dbbe7d6	account_mapping_search_by_parent_account_id	Lấy danh sách children account theo parent account	\N	WITH filtered_account_childrens AS (\r\n    SELECT \r\n        m.acc_srv_account_id,\r\n        m.parent_acc_srv_account_id,\r\n        m.acc_srv_account_group_id,\r\n        ca.full_name,\r\n        ca.username,\r\n        ca.created_at\r\n    FROM public.acc_srv_account_mapping m\r\n    INNER JOIN public.acc_srv_account ca ON m.acc_srv_account_id = ca.id\r\n    WHERE (\r\n        @keyword IS NULL \r\n        OR ca.full_name ILIKE '%' || @keyword || '%' \r\n        OR ca.username ILIKE '%' || @keyword || '%'\r\n    )\r\n    AND m.parent_acc_srv_account_id = @parent_account_id\r\n)\r\nSELECT \r\n    *,\r\n    COUNT(*) OVER() AS total_row\r\nFROM filtered_account_childrens\r\nORDER BY created_at DESC\r\nLIMIT @page_size \r\nOFFSET (@page_index * @page_size);	1	2026-02-05 03:11:38.304756+07	AccountService.AccountMappingService	3
ab9a7dbd-3033-4bae-899b-e8658773dfbb	account_dashboard_view_get_all_without_account_id	account_dashboard_view_get_all_without_account_id	\N	select * from vw_account_permission_extended_stats where account_id is null	1	2026-02-10 04:33:19.561225+07	HCare.AccountService.AccountDashboardViewService	3
978f706b-4740-46aa-973c-8126ae15072d	dashboard_order_get_by_priority_max	dashboard_order_get_by_priority_max	\N	SELECT *\r\nFROM public.vw_order_monthly_summary\r\nWHERE \r\n    priority <= @priority_max::int\r\n    AND (\r\n        @statuses IS NULL \r\n        OR order_status = ANY(@statuses::int4[])\r\n    )\r\nORDER BY \r\n    priority ASC, \r\n    order_status ASC	1	2026-02-25 07:05:43.057736+07	HCare.CrmService.ProductDashboardViewService	3
5e08d675-2475-41c3-bf8e-ef1642c70cd5	warehouse_product_transfer_search	warehouse_product_transfer_search	\N	WITH filtered AS (\r\n    SELECT\r\n        pb.*\r\n    FROM\r\n        crm_srv_product_batch pb\r\n            INNER JOIN crm_srv_product p ON pb.crm_srv_product_id = p.id and p.status = 1 \r\n            INNER JOIN crm_srv_warehouse w_to ON pb.crm_srv_warehouse_id_to = w_to.id and w_to.status = 1\r\n            LEFT JOIN crm_srv_warehouse w_from ON pb.crm_srv_warehouse_id_from = w_from.id and w_from.status = 1\r\n    WHERE\r\n        pb.is_active = true\r\n      AND (\r\n          NULLIF(@keyword, '') IS NULL\r\n              OR (\r\n              \t  pb.batch_name ILIKE '%' || @keyword || '%'\r\n                  OR w_from.name ILIKE '%' || @keyword || '%'\r\n                  OR w_to.name ILIKE '%' || @keyword || '%'\r\n              )\r\n        )\r\n      AND (@batch_ids IS null OR pb.batch_id = ANY(@batch_ids::uuid[]))\r\n      AND (@product_ids IS null OR pb.crm_srv_product_id = ANY(@product_ids::uuid[]))\r\n      AND (@warehouse_to_ids IS null OR pb.crm_srv_warehouse_id_to = ANY(@warehouse_to_ids::uuid[]))\r\n      AND (@warehouse_from_ids IS null OR pb.crm_srv_warehouse_id_from = ANY(@warehouse_from_ids::uuid[]))\r\n      AND (@start_date IS null OR pb.transit_date >= @start_date::timestamp with time zone)\r\n      AND (@end_date IS null OR pb.transit_date <= @end_date::timestamp with time zone)\r\n)\r\n   , counted AS (\r\n    SELECT COUNT(*) AS total_row\r\n    FROM filtered\r\n)\r\nSELECT\r\n    f.*,\r\n    c.total_row\r\nFROM\r\n    filtered f\r\n        CROSS JOIN counted c\r\nORDER BY\r\n    f.transit_date DESC,\r\n    f.created_at DESC\r\nLIMIT @page_size\r\n    OFFSET (@page_index * @page_size)	1	2026-01-29 08:06:19.276961+07	HCare.CrmService.WarehouseService	3
0060dcdf-5421-4f07-8507-03a08d99b691	order_search	order_search	\N	WITH filtered AS (SELECT cso.order_id,\r\n                         cso.address_id,\r\n                         cso.address_line,\r\n                         cso.order_status,\r\n                         cso.order_code,\r\n                         cso.account_id,\r\n                         cso.dealer_id,\r\n                         cso.created_at,\r\n                         cso.updated_at,\r\n                         cso.total_amount,\r\n                         cso.quantity,\r\n                         cso.unit_id,\r\n                         cso.payment_method,\r\n                         cso.order_note,\r\n                         cso.customer_name,\r\n                         cso.shipping_fee\r\n                  FROM crm_srv_order cso\r\n                  WHERE cso.order_status >= 1\r\n                    AND (@order_id IS NULL OR cso.order_id = @order_id)\r\n\r\n                    AND (\r\n                      @order_status IS NULL\r\n                          OR cso.order_status = @order_status\r\n                      )\r\n\r\n                    AND (\r\n                      NULLIF(@order_code, '') IS NULL\r\n                          OR cso.order_code = @order_code\r\n                      )\r\n\r\n                    AND (\r\n                      @start_date IS NULL\r\n                          OR cso.created_at >= @start_date\r\n                      )\r\n                    AND (\r\n                      @end_date IS NULL\r\n                          OR cso.created_at <= @end_date\r\n                      )\r\n\r\n                    AND (\r\n                      NULLIF(@search_keyword, '') IS NULL\r\n                          OR (\r\n                          cso.order_code ILIKE '%' || @search_keyword || '%'\r\n                              OR cso.address_line ILIKE '%' || @search_keyword || '%'\r\n                              or lower(cso.customer_name) ilike '%' || lower(@search_keyword) || '%'\r\n                          )\r\n                      )),\r\n\r\n     aggregated AS (SELECT order_id,\r\n                           address_id,\r\n                           address_line,\r\n                           order_status,\r\n                           order_code,\r\n                           account_id,\r\n                           dealer_id,\r\n                           MIN(created_at)            AS created_at,\r\n                           MAX(updated_at)            AS updated_at,\r\n                           SUM(total_amount)          AS total_amount,\r\n                           SUM(quantity)              AS total_quantity,\r\n                           unit_id,\r\n                           payment_method,\r\n                           max(order_note) as order_note,\r\n                           max(customer_name) as customer_name,\r\n                           max(shipping_fee) as shipping_fee\r\n                    FROM filtered\r\n                    GROUP BY order_id,\r\n                             address_id,\r\n                             address_line,\r\n                             order_status,\r\n                             order_code,\r\n                             account_id,\r\n                             dealer_id,\r\n                             unit_id,\r\n                             payment_method),\r\n\r\n     amount_filtered AS (SELECT a.*,\r\n                                (a.total_amount + coalesce(a.shipping_fee, 0)) as grand_total_amount\r\n                         FROM aggregated a\r\n                         WHERE (@start_amount IS NULL OR total_amount >= @start_amount)\r\n                           AND (@end_amount IS NULL OR total_amount <= @end_amount)),\r\n\r\n     count_total AS (SELECT COUNT(*) AS total_row\r\n                     FROM amount_filtered)\r\n\r\nSELECT af.*,\r\n       ct.total_row\r\nFROM amount_filtered af\r\n         CROSS JOIN count_total ct\r\nORDER BY af.order_code DESC\r\nOFFSET (@page_index * @page_size) LIMIT @page_size;\r\n	1	2026-01-19 15:24:08.091675+07	HCare.CrmService.OrderService	3
7542cb6c-b8f5-49ae-ad66-eb1157848737	formula_config_search	formula_config_search	\N	WITH filtered AS (\r\n    SELECT DISTINCT ON (fc.id)\r\n        fc.*\r\n    FROM public.formula_config fc\r\n    WHERE \r\n        (\r\n            NULLIF(@keyword, '') IS NULL\r\n            OR (\r\n                fc.code ILIKE '%' || @keyword || '%'\r\n                OR fc.table_name ILIKE '%' || @keyword || '%'\r\n                OR fc.table_column ILIKE '%' || @keyword || '%'\r\n                OR fc.prefix ILIKE '%' || @keyword || '%'\r\n                OR fc.suffix ILIKE '%' || @keyword || '%'\r\n            )\r\n        )\r\n    ORDER BY fc.id, fc.created_at DESC\r\n),\r\ncounted AS (\r\n    SELECT COUNT(*) AS total_row\r\n    FROM filtered\r\n)\r\nSELECT\r\n    f.*,\r\n    c.total_row\r\nFROM filtered f\r\nCROSS JOIN counted c\r\nORDER BY \r\n    f.created_at DESC,\r\n    f.code ASC\r\nLIMIT @page_size\r\nOFFSET @page_index * @page_size	1	2026-02-09 14:30:44.069607+07	Hcare.GeneralService.FormulaService	3
43dae05f-0f31-4862-8752-e9771b7d19a0	generic_formula_search	generic_formula_search	\N	WITH filtered AS (\r\n    SELECT DISTINCT ON (gf.id)\r\n        gf.*\r\n    FROM public.generic_formula gf\r\n    WHERE \r\n        (\r\n            NULLIF(@keyword, '') IS NULL\r\n            OR (\r\n                gf.fomula_name ILIKE '%' || @keyword || '%'\r\n                OR gf.regex_text ILIKE '%' || @keyword || '%'\r\n                OR gf.components ILIKE '%' || @keyword || '%'\r\n            )\r\n        )\r\n        AND (@formula_types IS NULL OR gf.block_type = ANY(@formula_types::int[]))\r\n        AND (@data_types IS NULL OR gf.data_type = ANY(@data_types::int[]))\r\n        AND (@logic_types IS NULL OR gf.logic_type = ANY(@logic_types::int[]))\r\n    ORDER BY gf.id, gf.fomula_name DESC\r\n),\r\ncounted AS (\r\n    SELECT COUNT(*) AS total_row\r\n    FROM filtered\r\n)\r\nSELECT\r\n    f.*,\r\n    c.total_row\r\nFROM filtered f\r\nCROSS JOIN counted c\r\nORDER BY \r\n    f.id, f.fomula_name ASC\r\nLIMIT @page_size\r\nOFFSET @page_index * @page_size	1	2026-02-09 14:24:32.842989+07	Hcare.GeneralService.FormulaService	3
e2689eb4-88ce-46f2-8ea8-792eae1fb24d	account_dashboard_view_get_by_permissions_and_stage_or_account_id	account_dashboard_view_get_by_permissions_and_stage_or_account_id	\N	WITH filtered AS (\r\n    SELECT v.*\r\n    FROM vw_account_permission_stage_stats v\r\n    WHERE \r\n        v.stage = @stage::int\r\n        AND (\r\n            @permissions::int[] IS NULL \r\n            OR EXISTS (\r\n                SELECT 1 \r\n                FROM unnest(@permissions::int[]) AS p \r\n                WHERE (v.permission & p) = p\r\n            )\r\n        )\r\n        AND v.account_id IS NOT DISTINCT FROM @account_id -- contain null val\r\n)\r\nSELECT \r\n    f.*,\r\n    (SELECT COUNT(*) FROM filtered) AS total_row \r\nFROM filtered f\r\nORDER BY \r\n    f.priority ASC, \r\n    f.total_users DESC\r\nLIMIT @page_size \r\nOFFSET (@page_index * @page_size)	1	2026-02-27 05:56:29.755698+07	HCare.AccountService.AccountDashboardViewService	3
e74f092a-610e-4732-91f4-0ddf46e80f4e	warehouse_product_export_search	warehouse_product_export_search	\N	\r\nWITH filtered AS (\r\n    SELECT DISTINCT ON (pj.crm_srv_product_id, pj.serial_number)\r\n        pj.*\r\n    FROM crm_srv_produced_journey pj\r\n        JOIN crm_srv_product prd\r\n            ON prd.status = 1\r\n           AND pj.crm_srv_product_id = prd.id\r\n--         JOIN crm_srv_dealer dealer_to\r\n--             ON dealer_to.status = 1\r\n--            AND pj.crm_srv_dealer_id = dealer_to.id\r\n        JOIN crm_srv_warehouse w_from\r\n            ON w_from.status = 1\r\n           AND pj.crm_srv_warehouse_id = w_from.id\r\n    WHERE (pj.journey_status = 4 or pj.journey_status = 6 )\r\n      AND (\r\n          NULLIF(@keyword, '') IS NULL\r\n          OR (\r\n              prd.name ILIKE '%' || @keyword || '%'\r\n              OR pj.serial_number ILIKE '%' || @keyword || '%'\r\n          )\r\n      )\r\n      AND (@product_ids IS NULL OR pj.crm_srv_product_id = ANY(@product_ids::uuid[]))\r\n      AND (@dealer_to_ids IS NULL OR pj.crm_srv_dealer_id = ANY(@dealer_to_ids::uuid[]))\r\n      AND (@warehouse_from_ids IS NULL OR pj.crm_srv_warehouse_id = ANY(@warehouse_from_ids::uuid[]))\r\n      AND (@start_date IS NULL OR pj.created_at >= @start_date::timestamptz)\r\n      AND (@end_date IS NULL OR pj.created_at <= @end_date::timestamptz)\r\n    ORDER BY pj.crm_srv_product_id, pj.serial_number, pj.created_at DESC\r\n),\r\ncounted AS (\r\n    SELECT COUNT(*) AS total_row\r\n    FROM filtered\r\n)\r\nSELECT\r\n    f.*,\r\n    c.total_row\r\nFROM filtered f\r\nCROSS JOIN counted c\r\nORDER BY\r\n    f.crm_srv_product_id ASC,\r\n    f.journey_date DESC,\r\n    f.created_at DESC\r\nLIMIT @page_size\r\nOFFSET @page_index * @page_size	1	2026-02-02 15:46:09.780449+07	HCare.CrmService.WarehouseService	3
\.


--
-- Name: fld_query_master fld_query_master_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.fld_query_master
    ADD CONSTRAINT fld_query_master_pkey PRIMARY KEY (id);


--
-- Name: formula_config formula_config_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.formula_config
    ADD CONSTRAINT formula_config_pkey PRIMARY KEY (id);


--
-- Name: generic_formula generic_formula_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.generic_formula
    ADD CONSTRAINT generic_formula_pkey PRIMARY KEY (id);


--
-- Name: tblmaster tblmaster_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.tblmaster
    ADD CONSTRAINT tblmaster_pkey PRIMARY KEY (id);


--
-- Name: formula_config uk_formula_config_code; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.formula_config
    ADD CONSTRAINT uk_formula_config_code UNIQUE (code);


--
-- Name: formula_config uk_table_column; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.formula_config
    ADD CONSTRAINT uk_table_column UNIQUE (table_name, table_column);


--
-- Name: fld_query_master uq_fld_query_master_tblmaster_id_field_name; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.fld_query_master
    ADD CONSTRAINT uq_fld_query_master_tblmaster_id_field_name UNIQUE (tblmaster_id, field_name);


--
-- Name: tblmaster uq_tblmaster_code; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.tblmaster
    ADD CONSTRAINT uq_tblmaster_code UNIQUE (code);


--
-- Name: tblmaster uq_tblmaster_execfunc; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.tblmaster
    ADD CONSTRAINT uq_tblmaster_execfunc UNIQUE (execfunc);


--
-- Name: idx_fld_query_master_tblmaster_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_fld_query_master_tblmaster_id ON public.fld_query_master USING btree (tblmaster_id);


--
-- Name: idx_fld_query_master_tblmaster_id_field_name; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_fld_query_master_tblmaster_id_field_name ON public.fld_query_master USING btree (tblmaster_id, field_name);


--
-- Name: idx_generic_formula_name_unique; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX idx_generic_formula_name_unique ON public.generic_formula USING btree (fomula_name);


--
-- Name: idx_tblmaster_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_tblmaster_code ON public.tblmaster USING btree (code);


--
-- Name: idx_tblmaster_exectype; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_tblmaster_exectype ON public.tblmaster USING btree (id, exectype);


--
-- PostgreSQL database dump complete
--

\unrestrict YrBjzLapw78g6f5ymOxwIzSmdccPfC5uV9rbGVrDJMkH4IXNGlweYy4hDPfMyMe

