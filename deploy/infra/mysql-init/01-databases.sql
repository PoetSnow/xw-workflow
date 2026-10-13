-- 首次启动 MySQL 容器时自动建空库（表结构请再执行 docs/sql 下一库一脚本）
CREATE DATABASE IF NOT EXISTS `nx_devices_erp` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS `nx_devices_erp_log` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS `xw_brain` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
-- 历史别名，可忽略
CREATE DATABASE IF NOT EXISTS `xw` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS `xw_syslog` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
