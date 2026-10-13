/*
 Navicat Premium Dump SQL

 Source Server         : 本机
 Source Server Type    : MySQL
 Source Server Version : 80046 (8.0.46)
 Source Host           : localhost:3306
 Source Schema         : xw_workflow_log

 Target Server Type    : MySQL
 Target Server Version : 80046 (8.0.46)
 File Encoding         : 65001

 Date: 13/10/2026 16:47:23
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for login_log
-- ----------------------------
DROP TABLE IF EXISTS `login_log`;
CREATE TABLE `login_log`  (
  `id` bigint NOT NULL COMMENT '主键',
  `device` varchar(256) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '登录设备',
  `message` varchar(1024) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '消息',
  `succeed` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否成功',
  `statuscode` int NOT NULL DEFAULT 0 COMMENT 'HTTP 状态码',
  `userid` bigint NOT NULL DEFAULT 0 COMMENT '用户 Id',
  `account` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '账号',
  `name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '姓名',
  `remoteipaddress` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '客户端 IP',
  `executiontime` int NOT NULL DEFAULT 0 COMMENT '耗时(ms)',
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_login_log_userid_createtime`(`userid` ASC, `createtime` ASC) USING BTREE,
  INDEX `ix_login_log_account_createtime`(`account` ASC, `createtime` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '登录日志' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of login_log
-- ----------------------------
INSERT INTO `login_log` VALUES (858965935267973, 'web', 'Login succeeded', 1, 201, 910000000000501, 'admin', '管理员', '0.0.0.1', 606, '2026-10-13 14:41:22.310700');

-- ----------------------------
-- Table structure for operation_log
-- ----------------------------
DROP TABLE IF EXISTS `operation_log`;
CREATE TABLE `operation_log`  (
  `id` bigint NOT NULL COMMENT '主键',
  `classname` varchar(256) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT 'Controller 类名',
  `logname` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '业务日志名',
  `logtype` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '日志类型',
  `message` mediumtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '详情',
  `method` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '方法名',
  `succeed` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否成功',
  `userid` bigint NOT NULL DEFAULT 0 COMMENT '操作人 Id',
  `account` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '账号',
  `name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '姓名',
  `remoteipaddress` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '客户端 IP',
  `executiontime` int NOT NULL DEFAULT 0 COMMENT '耗时(ms)',
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_operation_log_userid_createtime`(`userid` ASC, `createtime` ASC) USING BTREE,
  INDEX `ix_operation_log_classname_createtime`(`classname` ASC, `createtime` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '操作日志' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of operation_log
-- ----------------------------
INSERT INTO `operation_log` VALUES (858966171897989, 'XW.System.Application.Contracts.Interfaces.IMenuService', 'Update menu', 'Operation Log', '[920442132822001,{\"parentId\":0,\"name\":\"智能助手\",\"perm\":\"\",\"routeName\":\"AgentCenter\",\"routePath\":\"/agent\",\"type\":\"CATALOG\",\"component\":\"Layout\",\"visible\":true,\"redirect\":\"agent-chat\",\"icon\":\"visitor\",\"keepAlive\":false,\"alwaysShow\":true,\"params\":[],\"ordinal\":48}]', 'UpdateAsync', 1, 910000000000501, 'admin', '管理员', '0.0.0.1', 253, '2026-10-13 14:42:20.081934');

SET FOREIGN_KEY_CHECKS = 1;
