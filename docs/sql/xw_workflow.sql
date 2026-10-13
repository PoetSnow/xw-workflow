/*
 Navicat Premium Dump SQL

 Source Server         : 本机
 Source Server Type    : MySQL
 Source Server Version : 80046 (8.0.46)
 Source Host           : localhost:3306
 Source Schema         : xw_workflow

 Target Server Type    : MySQL
 Target Server Version : 80046 (8.0.46)
 File Encoding         : 65001

 Date: 13/10/2026 16:47:11
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for appr_business_field
-- ----------------------------
DROP TABLE IF EXISTS `appr_business_field`;
CREATE TABLE `appr_business_field`  (
  `id` bigint NOT NULL,
  `businesstype` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'contract / project / budget / purchase',
  `fieldkey` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '快照 JSON 的键，如 amount',
  `fieldname` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '界面展示名',
  `fieldtype` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Number / String / Enum / User / Department',
  `valuesource` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Form' COMMENT 'Form / System / Derived',
  `fieldconfig` json NULL COMMENT 'ENUM options 等',
  `required` tinyint(1) NOT NULL DEFAULT 0,
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  `ordinal` int NOT NULL DEFAULT 0,
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `modifyby` bigint NOT NULL DEFAULT 0,
  `modifytime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_appr_business_field`(`businesstype` ASC, `fieldkey` ASC) USING BTREE,
  INDEX `ix_appr_business_field_type`(`businesstype` ASC, `enabled` ASC, `ordinal` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '审批条件用的业务字段定义' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_business_field
-- ----------------------------

-- ----------------------------
-- Table structure for appr_condition_group
-- ----------------------------
DROP TABLE IF EXISTS `appr_condition_group`;
CREATE TABLE `appr_condition_group`  (
  `id` bigint NOT NULL,
  `ownertype` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Transition / Binding',
  `ownerid` bigint NOT NULL COMMENT 'transition.id 或 binding.id',
  `parentgroupid` bigint NULL DEFAULT NULL COMMENT '→ appr_condition_group.id，根为 NULL',
  `groupname` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `grouplogic` varchar(8) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'And / Or',
  `grouptype` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Root' COMMENT 'Root / Group',
  `ordinal` int NOT NULL DEFAULT 0,
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `modifyby` bigint NOT NULL DEFAULT 0,
  `modifytime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_appr_condition_owner`(`ownertype` ASC, `ownerid` ASC, `parentgroupid` ASC, `ordinal` ASC) USING BTREE,
  INDEX `ix_appr_condition_parent`(`parentgroupid` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '审批条件组' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_condition_group
-- ----------------------------

-- ----------------------------
-- Table structure for appr_condition_item
-- ----------------------------
DROP TABLE IF EXISTS `appr_condition_item`;
CREATE TABLE `appr_condition_item`  (
  `id` bigint NOT NULL,
  `groupid` bigint NOT NULL COMMENT '→ appr_condition_group.id',
  `fieldkey` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `operator` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '= != > >= < <= In NotIn Contains',
  `value` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `valuetype` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Const',
  `ordinal` int NOT NULL DEFAULT 0,
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `modifyby` bigint NOT NULL DEFAULT 0,
  `modifytime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_appr_condition_item_group`(`groupid` ASC, `ordinal` ASC) USING BTREE,
  INDEX `ix_appr_condition_item_field`(`fieldkey` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '审批条件项' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_condition_item
-- ----------------------------

-- ----------------------------
-- Table structure for appr_history
-- ----------------------------
DROP TABLE IF EXISTS `appr_history`;
CREATE TABLE `appr_history`  (
  `id` bigint NOT NULL,
  `approveinstanceid` bigint NOT NULL DEFAULT 0 COMMENT '→ appr_instance.id',
  `instanceid` bigint NOT NULL COMMENT '→ appr_instance.id',
  `instancenodeid` bigint NULL DEFAULT NULL COMMENT '→ appr_instance_node.id',
  `taskid` bigint NULL DEFAULT NULL COMMENT '→ appr_task.id',
  `operatoruserid` bigint NOT NULL COMMENT '→ sys_user.id',
  `action` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Submit / Approve / Reject / Transfer / Delegate / Cancel / Skip / Complete / Read',
  `comment` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `snapshot` json NULL COMMENT '当时节点、审批人、条件等审计快照',
  `operatetime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_appr_history_instance`(`instanceid` ASC, `operatetime` ASC) USING BTREE,
  INDEX `ix_appr_history_operator`(`operatoruserid` ASC) USING BTREE,
  INDEX `ix_appr_history_node`(`instancenodeid` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '审批操作历史' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_history
-- ----------------------------

-- ----------------------------
-- Table structure for appr_instance
-- ----------------------------
DROP TABLE IF EXISTS `appr_instance`;
CREATE TABLE `appr_instance`  (
  `id` bigint NOT NULL,
  `processdefid` bigint NOT NULL COMMENT '→ appr_process_def.id 冻结版本',
  `businesstype` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `businessid` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '业务主键，兼容数字/单号/UUID',
  `title` varchar(256) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `submituserid` bigint NOT NULL COMMENT '→ sys_user.id',
  `submitdeptid` bigint NOT NULL COMMENT '提交时发起人部门 → sys_organization.id',
  `currentnodeid` bigint NULL DEFAULT NULL COMMENT '→ appr_instance_node.id',
  `status` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Running' COMMENT 'Running / Approved / Rejected / Cancelled / Terminated',
  `formsnapshot` json NOT NULL COMMENT '提交时表单快照',
  `starttime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `endtime` datetime(6) NULL DEFAULT NULL,
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `modifyby` bigint NOT NULL DEFAULT 0,
  `modifytime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_appr_instance_business`(`businesstype` ASC, `businessid` ASC, `status` ASC) USING BTREE,
  INDEX `ix_appr_instance_process`(`processdefid` ASC) USING BTREE,
  INDEX `ix_appr_instance_submit`(`submituserid` ASC, `status` ASC) USING BTREE,
  INDEX `ix_appr_instance_status`(`status` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '审批实例' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_instance
-- ----------------------------

-- ----------------------------
-- Table structure for appr_instance_node
-- ----------------------------
DROP TABLE IF EXISTS `appr_instance_node`;
CREATE TABLE `appr_instance_node`  (
  `id` bigint NOT NULL,
  `approveinstanceid` bigint NOT NULL DEFAULT 0 COMMENT '→ appr_instance.id',
  `instanceid` bigint NOT NULL COMMENT '→ appr_instance.id',
  `processnodeid` bigint NOT NULL COMMENT '→ appr_process_node.id',
  `nodekey` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `nodename` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `nodetype` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '提交时从定义拷贝',
  `approvemode` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `status` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Pending' COMMENT 'Pending / Running / Approved / Rejected / Skipped / Cancelled',
  `passruletype` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `passrulevalue` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `rejectruletype` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `rejectrulevalue` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `starttime` datetime(6) NULL DEFAULT NULL,
  `endtime` datetime(6) NULL DEFAULT NULL,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_appr_instance_node_instance`(`instanceid` ASC, `status` ASC) USING BTREE,
  INDEX `ix_appr_instance_node_process`(`processnodeid` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '审批实例节点快照' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_instance_node
-- ----------------------------

-- ----------------------------
-- Table structure for appr_process_binding
-- ----------------------------
DROP TABLE IF EXISTS `appr_process_binding`;
CREATE TABLE `appr_process_binding`  (
  `id` bigint NOT NULL,
  `businesstype` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `businesssubtype` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `processdefid` bigint NOT NULL COMMENT '→ appr_process_def.id 具体版本',
  `priority` int NOT NULL DEFAULT 0,
  `isdefault` tinyint(1) NOT NULL DEFAULT 0,
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `modifyby` bigint NOT NULL DEFAULT 0,
  `modifytime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_appr_binding_business`(`businesstype` ASC, `enabled` ASC, `priority` ASC) USING BTREE,
  INDEX `ix_appr_binding_process`(`processdefid` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '业务与流程版本绑定' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_process_binding
-- ----------------------------

-- ----------------------------
-- Table structure for appr_process_def
-- ----------------------------
DROP TABLE IF EXISTS `appr_process_def`;
CREATE TABLE `appr_process_def`  (
  `id` bigint NOT NULL,
  `processkey` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `processname` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `businesstype` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `version` int NOT NULL DEFAULT 1,
  `status` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Draft' COMMENT 'Draft / Active / Disabled',
  `layoutjson` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '画布节点坐标，不参与流转',
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `modifyby` bigint NOT NULL DEFAULT 0,
  `modifytime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_appr_process_version`(`processkey` ASC, `version` ASC) USING BTREE,
  INDEX `ix_appr_process_business`(`businesstype` ASC, `status` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '审批流程定义' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_process_def
-- ----------------------------

-- ----------------------------
-- Table structure for appr_process_node
-- ----------------------------
DROP TABLE IF EXISTS `appr_process_node`;
CREATE TABLE `appr_process_node`  (
  `id` bigint NOT NULL,
  `processdefid` bigint NOT NULL COMMENT '→ appr_process_def.id',
  `nodekey` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `nodename` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `nodetype` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Start / Approve / Collaborate / Notify / Task / End',
  `approvemode` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT 'Single / OrSign / AndSign / Vote',
  `passruletype` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT 'All / Any / Percent',
  `passrulevalue` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `rejectruletype` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT 'AnyReject',
  `rejectrulevalue` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `formschema` json NULL COMMENT '本环默认交付清单',
  `ordinal` int NOT NULL DEFAULT 0 COMMENT '仅展示顺序，不参与流转',
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `modifyby` bigint NOT NULL DEFAULT 0,
  `modifytime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_appr_process_node_key`(`processdefid` ASC, `nodekey` ASC) USING BTREE,
  INDEX `ix_appr_process_node_def`(`processdefid` ASC, `ordinal` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '审批流程节点' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_process_node
-- ----------------------------

-- ----------------------------
-- Table structure for appr_process_transition
-- ----------------------------
DROP TABLE IF EXISTS `appr_process_transition`;
CREATE TABLE `appr_process_transition`  (
  `id` bigint NOT NULL,
  `processdefid` bigint NOT NULL COMMENT '→ appr_process_def.id',
  `fromnodeid` bigint NOT NULL COMMENT '→ appr_process_node.id',
  `tonodeid` bigint NOT NULL COMMENT '→ appr_process_node.id',
  `name` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `priority` int NOT NULL DEFAULT 0,
  `isdefault` tinyint(1) NOT NULL DEFAULT 0 COMMENT '本节点其它条件都未命中时走这条',
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `modifyby` bigint NOT NULL DEFAULT 0,
  `modifytime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_appr_transition_from`(`processdefid` ASC, `fromnodeid` ASC, `priority` ASC) USING BTREE,
  INDEX `ix_appr_transition_to`(`tonodeid` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '审批流程连线' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_process_transition
-- ----------------------------

-- ----------------------------
-- Table structure for appr_resolver
-- ----------------------------
DROP TABLE IF EXISTS `appr_resolver`;
CREATE TABLE `appr_resolver`  (
  `id` bigint NOT NULL,
  `processnodeid` bigint NOT NULL COMMENT '→ appr_process_node.id',
  `resolvertype` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Role / RoleInDept / User',
  `resolvervalue` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '角色 code 或 sys_user.id',
  `resolverext` json NULL COMMENT 'RoleInDept 时 DeptFrom=Business|Initiator',
  `formschema` json NULL COMMENT '覆盖节点默认清单；空则用节点 formschema',
  `ordinal` int NOT NULL DEFAULT 0,
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `modifyby` bigint NOT NULL DEFAULT 0,
  `modifytime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_appr_resolver_node`(`processnodeid` ASC, `ordinal` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '节点审批人解析规则' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_resolver
-- ----------------------------

-- ----------------------------
-- Table structure for appr_task
-- ----------------------------
DROP TABLE IF EXISTS `appr_task`;
CREATE TABLE `appr_task`  (
  `id` bigint NOT NULL,
  `approveinstanceid` bigint NOT NULL DEFAULT 0 COMMENT '→ appr_instance.id',
  `instanceid` bigint NOT NULL COMMENT '→ appr_instance.id',
  `instancenodeid` bigint NOT NULL COMMENT '→ appr_instance_node.id',
  `assigneeuserid` bigint NULL DEFAULT NULL COMMENT '→ sys_user.id',
  `deptid` bigint NULL DEFAULT NULL COMMENT '解析时部门 → sys_organization.id',
  `positionid` bigint NULL DEFAULT NULL COMMENT '解析时岗位 → sys_position.id',
  `tasktype` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Approve' COMMENT 'Approve / Complete / Read',
  `status` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Pending' COMMENT 'Pending / Approved / Rejected / Cancelled / Transferred / Skipped',
  `comment` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `payload` json NULL COMMENT '本待办提交的字段 JSON',
  `parenttaskid` bigint NULL DEFAULT NULL COMMENT '转交/加签预留',
  `transferfromuserid` bigint NULL DEFAULT NULL,
  `delegateuserid` bigint NULL DEFAULT NULL,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `completetime` datetime(6) NULL DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_appr_task_instance`(`instanceid` ASC) USING BTREE,
  INDEX `ix_appr_task_node`(`instancenodeid` ASC) USING BTREE,
  INDEX `ix_appr_task_user_status`(`assigneeuserid` ASC, `status` ASC) USING BTREE,
  INDEX `ix_appr_task_position`(`positionid` ASC, `deptid` ASC, `status` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '审批待办任务' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_task
-- ----------------------------

-- ----------------------------
-- Table structure for appr_task_file
-- ----------------------------
DROP TABLE IF EXISTS `appr_task_file`;
CREATE TABLE `appr_task_file`  (
  `id` bigint NOT NULL,
  `taskid` bigint NOT NULL COMMENT '→ appr_task.id',
  `instanceid` bigint NOT NULL COMMENT '→ appr_instance.id',
  `filename` varchar(256) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `contenttype` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `filesize` bigint NOT NULL DEFAULT 0,
  `storagekey` varchar(512) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '对象存储路径或本地键',
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `ix_appr_task_file_task`(`taskid` ASC) USING BTREE,
  INDEX `ix_appr_task_file_instance`(`instanceid` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '协作/任务待办附件' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of appr_task_file
-- ----------------------------

-- ----------------------------
-- Table structure for sys_config
-- ----------------------------
DROP TABLE IF EXISTS `sys_config`;
CREATE TABLE `sys_config`  (
  `id` bigint NOT NULL,
  `createby` bigint NOT NULL,
  `createtime` datetime(6) NOT NULL,
  `modifyby` bigint NOT NULL,
  `modifytime` datetime(6) NOT NULL,
  `key` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Parameter key',
  `name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Parameter name',
  `value` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Parameter value',
  `remark` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Remark',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'System parameter' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_config
-- ----------------------------
INSERT INTO `sys_config` VALUES (654337157616325, 653335112912901, '2025-03-14 09:24:44.641988', 653335112912901, '2025-03-14 09:24:44.642019', 'weixin-key', '微信接口Key', '343sfsdfas', '微信接口配置');
INSERT INTO `sys_config` VALUES (910000000000201, 0, '2026-09-30 17:03:26.969327', 0, '2026-09-30 17:03:26.969327', 'diagnosis.brain.useMock', '三合一走mock', 'true', 'false 时调用 Brain');
INSERT INTO `sys_config` VALUES (910000000000202, 0, '2026-09-30 17:03:26.971575', 0, '2026-09-30 17:03:26.971575', 'diagnosis.brain.baseUrl', 'Brain地址', 'http://127.0.0.1:5001', '调理评估');
INSERT INTO `sys_config` VALUES (910000000000203, 0, '2026-09-30 17:03:26.973722', 0, '2026-09-30 17:03:26.973722', 'diagnosis.wxacode.mock', '小程序码mock', 'true', '三合一报告携带');
INSERT INTO `sys_config` VALUES (910000000000204, 0, '2026-09-30 17:03:26.975829', 0, '2026-09-30 17:03:26.975829', 'wx.mini.appId', '小程序AppId', '', '微信登录');
INSERT INTO `sys_config` VALUES (910000000000205, 0, '2026-09-30 17:03:26.977871', 0, '2026-09-30 17:03:26.977871', 'wx.mini.appSecret', '小程序密钥', '', '只在服务端使用');
INSERT INTO `sys_config` VALUES (910000000000206, 0, '2026-09-30 17:03:26.980086', 0, '2026-09-30 17:03:26.980086', 'wx.subscribe.enabled', '订阅开关', 'false', 'true 才提示订阅');
INSERT INTO `sys_config` VALUES (910000000000207, 0, '2026-09-30 17:03:26.982100', 0, '2026-09-30 17:03:26.982100', 'wx.subscribe.templateId', '订阅模板', '', '模板字段以后再配');
INSERT INTO `sys_config` VALUES (910000000000208, 0, '2026-09-30 17:03:26.984049', 0, '2026-09-30 17:03:26.984049', 'mall.unpaid.expireMinutes', '待支付超时', '30', '分钟，延迟消息与兜底扫表共用');

-- ----------------------------
-- Table structure for sys_dictionary
-- ----------------------------
DROP TABLE IF EXISTS `sys_dictionary`;
CREATE TABLE `sys_dictionary`  (
  `id` bigint NOT NULL,
  `createby` bigint NOT NULL,
  `createtime` datetime(6) NOT NULL,
  `modifyby` bigint NOT NULL,
  `modifytime` datetime(6) NOT NULL,
  `code` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `remark` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `status` tinyint(1) NOT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'Dictionary' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_dictionary
-- ----------------------------
INSERT INTO `sys_dictionary` VALUES (654231800692741, 653335112912901, '2025-03-14 02:16:02.842381', 653335112912901, '2025-03-14 02:16:02.847194', 'notice_type', '通知类型', '', 1);
INSERT INTO `sys_dictionary` VALUES (654240219889733, 653335112912901, '2025-03-14 02:50:18.256197', 653335112912901, '2025-03-14 02:50:50.768281', 'notice_level', '通知级别', '', 1);
INSERT INTO `sys_dictionary` VALUES (657672185329605, 653335112912901, '2025-03-23 19:35:00.432169', 653335112912901, '2025-03-23 19:37:07.391462', 'exchange_behavior', '客户金额变动类型', '', 1);
INSERT INTO `sys_dictionary` VALUES (657672310503365, 653335112912901, '2025-03-23 19:35:30.952618', 653335112912901, '2025-03-23 19:37:20.731602', 'exchage_status', '客户金额变动状态', '', 1);

-- ----------------------------
-- Table structure for sys_dictionary_data
-- ----------------------------
DROP TABLE IF EXISTS `sys_dictionary_data`;
CREATE TABLE `sys_dictionary_data`  (
  `id` bigint NOT NULL,
  `createby` bigint NOT NULL,
  `createtime` datetime(6) NOT NULL,
  `modifyby` bigint NOT NULL,
  `modifytime` datetime(6) NOT NULL,
  `dictcode` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `label` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `value` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `tagtype` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `status` tinyint(1) NOT NULL,
  `ordinal` int NOT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'Dictionary data' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_dictionary_data
-- ----------------------------
INSERT INTO `sys_dictionary_data` VALUES (654239258492997, 653335112912901, '2025-03-14 02:46:23.753258', 653335112912901, '2025-03-14 02:46:23.764096', 'notice_type', '系统升级', '1', 'success', 1, 1);
INSERT INTO `sys_dictionary_data` VALUES (654239345217605, 653335112912901, '2025-03-14 02:46:44.662244', 653335112912901, '2025-03-14 02:46:44.662315', 'notice_type', '系统维护', '2', 'warning', 1, 2);
INSERT INTO `sys_dictionary_data` VALUES (654239670661189, 653335112912901, '2025-03-14 02:48:04.130043', 653335112912901, '2025-03-14 02:48:17.928995', 'notice_type', '安全警告', '3', 'info', 1, 3);
INSERT INTO `sys_dictionary_data` VALUES (654239799701573, 653335112912901, '2025-03-14 02:48:35.619266', 653335112912901, '2025-03-14 02:48:35.619300', 'notice_type', '假期通知', '4', 'primary', 1, 4);
INSERT INTO `sys_dictionary_data` VALUES (654239873306693, 653335112912901, '2025-03-14 02:48:53.589871', 653335112912901, '2025-03-14 02:48:53.589908', 'notice_type', '公司新闻', '5', 'danger', 1, 5);
INSERT INTO `sys_dictionary_data` VALUES (654240025727045, 653335112912901, '2025-03-14 02:49:30.801847', 653335112912901, '2025-03-14 02:49:47.775236', 'notice_type', '其他', '7', 'info', 1, 99);
INSERT INTO `sys_dictionary_data` VALUES (654241282986053, 653335112912901, '2025-03-14 02:54:37.749547', 653335112912901, '2025-03-14 02:54:37.749570', 'notice_level', '高', 'H', 'danger', 1, 1);
INSERT INTO `sys_dictionary_data` VALUES (654241329270853, 653335112912901, '2025-03-14 02:54:49.049727', 653335112912901, '2025-03-14 02:55:31.410027', 'notice_level', '中', 'M', 'primary', 1, 2);
INSERT INTO `sys_dictionary_data` VALUES (654241459761221, 653335112912901, '2025-03-14 02:55:20.908143', 653335112912901, '2025-03-14 02:55:40.136094', 'notice_level', '低', 'L', 'info', 1, 3);
INSERT INTO `sys_dictionary_data` VALUES (657672538613701, 653335112912901, '2025-03-23 19:36:26.683774', 653335112912901, '2025-03-25 12:55:08.585748', 'exchange_behavior', '后台充值', '8000', 'success', 1, 0);
INSERT INTO `sys_dictionary_data` VALUES (657672875403205, 653335112912901, '2025-03-23 19:37:48.899555', 653335112912901, '2025-03-23 19:38:30.251295', 'exchage_status', '处理中', '2000', 'primary', 1, 0);
INSERT INTO `sys_dictionary_data` VALUES (657672927889349, 653335112912901, '2025-03-23 19:38:01.680144', 653335112912901, '2025-03-23 19:38:44.726467', 'exchage_status', '已完成', '2008', 'success', 1, 3);
INSERT INTO `sys_dictionary_data` VALUES (657673015670725, 653335112912901, '2025-03-23 19:38:23.111797', 653335112912901, '2025-03-23 19:38:48.499734', 'exchage_status', '已失败', '2016', 'danger', 1, 6);
INSERT INTO `sys_dictionary_data` VALUES (658281065769541, 653335112912901, '2025-03-25 12:52:32.845266', 653335112912901, '2025-03-25 12:55:21.807517', 'exchange_behavior', '订单交易', '8008', 'primary', 1, 1);

-- ----------------------------
-- Table structure for sys_eventtracker
-- ----------------------------
DROP TABLE IF EXISTS `sys_eventtracker`;
CREATE TABLE `sys_eventtracker`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `eventid` bigint NOT NULL,
  `trackername` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `createby` bigint NOT NULL,
  `createtime` datetime(6) NOT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `ix_sys_eventtracker_eventid_trackername`(`eventid` ASC, `trackername` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_eventtracker
-- ----------------------------

-- ----------------------------
-- Table structure for sys_menu
-- ----------------------------
DROP TABLE IF EXISTS `sys_menu`;
CREATE TABLE `sys_menu`  (
  `id` bigint NOT NULL,
  `createby` bigint NOT NULL,
  `createtime` datetime(6) NOT NULL,
  `modifyby` bigint NOT NULL,
  `modifytime` datetime(6) NOT NULL,
  `parentid` bigint NOT NULL COMMENT 'Parent menu ID',
  `parentids` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Parent menu ID path',
  `name` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Name',
  `perm` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Permission code',
  `routename` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Route name',
  `routepath` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Route path',
  `type` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Menu type',
  `component` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Component configuration',
  `visible` tinyint(1) NOT NULL COMMENT 'Visible',
  `redirect` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Redirect route path',
  `icon` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Icon',
  `keepalive` tinyint(1) NOT NULL COMMENT 'Enable page caching',
  `alwaysshow` tinyint(1) NOT NULL COMMENT 'Always show when there is only one child route',
  `params` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Route parameters',
  `ordinal` int NOT NULL COMMENT 'Ordinal',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'Menu' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_menu
-- ----------------------------
INSERT INTO `sys_menu` VALUES (653342080185029, 1000000000000, '2025-03-11 13:55:46.028479', 653335112912901, '2026-06-08 11:56:38.188649', 0, '[0]', '系统管理', '', '', '/system', 'CATALOG', 'Layout', 1, '/system/user', 'role', 1, 1, '', 10);
INSERT INTO `sys_menu` VALUES (653342584296133, 1000000000000, '2025-03-11 13:57:49.016162', 653335112912901, '2025-03-14 09:10:34.425720', 653342080185029, '[0][653342080185029]', '菜单管理', '', 'Menu', 'menu', 'MENU', 'system/menu/index', 1, '', 'menu', 1, 0, '', 4);
INSERT INTO `sys_menu` VALUES (653343418946245, 1000000000000, '2025-03-11 14:01:12.781334', 653335112912901, '2025-03-12 02:18:57.594467', 653342584296133, '[0][653342080185029][653342584296133]', '菜单新增', 'menu-create', '', '', 'BUTTON', '', 1, '', '', 1, 1, '', 1);
INSERT INTO `sys_menu` VALUES (653389651534725, 653335112912901, '2025-03-11 17:09:20.259624', 653335112912901, '2025-03-12 02:18:51.063178', 653342584296133, '[0][653342080185029][653342584296133]', '菜单修改', 'menu-update', '', '', 'BUTTON', '', 1, '', '', 1, 0, '111=111', 2);
INSERT INTO `sys_menu` VALUES (653524636169093, 653335112912901, '2025-03-12 02:18:35.485594', 653335112912901, '2025-03-12 02:18:35.490303', 653342584296133, '[0][653342080185029][653342584296133]', '菜单删除', 'menu-delete', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 3);
INSERT INTO `sys_menu` VALUES (653525628846981, 653335112912901, '2025-03-12 02:22:37.641550', 653335112912901, '2025-03-12 17:18:54.181204', 653342080185029, '[0][653342080185029]', '角色管理', '', 'Role', 'role', 'MENU', 'system/role/index', 1, '', 'el-icon-Trophy', 1, 0, '', 2);
INSERT INTO `sys_menu` VALUES (653525880882053, 653335112912901, '2025-03-12 02:23:39.176354', 653335112912901, '2025-03-12 02:23:39.176387', 653525628846981, '[0][653342080185029][653525628846981]', '角色新增', 'role-create', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (653525982651269, 653335112912901, '2025-03-12 02:24:04.021653', 653335112912901, '2025-03-12 02:24:04.021694', 653525628846981, '[0][653342080185029][653525628846981]', '角色修改', 'role-update', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 2);
INSERT INTO `sys_menu` VALUES (653526164857733, 653335112912901, '2025-03-12 02:24:48.546878', 653335112912901, '2025-03-12 02:24:48.546926', 653525628846981, '[0][653342080185029][653525628846981]', '角色删除', 'role-delete', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 3);
INSERT INTO `sys_menu` VALUES (653526703580037, 653335112912901, '2025-03-12 02:27:00.032542', 653335112912901, '2025-03-15 01:31:28.153432', 653525628846981, '[0][653342080185029][653525628846981]', '角色查询', 'role-search', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 4);
INSERT INTO `sys_menu` VALUES (653688386045317, 653335112912901, '2025-03-12 13:24:53.568100', 1000000000000, '2026-10-06 14:16:58.460585', 653342080185029, '[0][653342080185029]', '机构管理', '', 'Dept', 'dept', 'MENU', 'system/dept/index', 1, '', 'el-icon-CoffeeCup', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (653688962147717, 653335112912901, '2025-03-12 13:27:13.929212', 653335112912901, '2025-03-12 13:27:13.929284', 653688386045317, '[0][653342080185029][653688386045317]', '机构新增', 'org-create', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (653689048724869, 653335112912901, '2025-03-12 13:27:35.065392', 653335112912901, '2025-03-12 13:29:55.810715', 653688386045317, '[0][653342080185029][653688386045317]', '机构修改', 'org-update', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 2);
INSERT INTO `sys_menu` VALUES (653689299236229, 653335112912901, '2025-03-12 13:28:36.228932', 653335112912901, '2025-03-12 13:30:04.669707', 653688386045317, '[0][653342080185029][653688386045317]', '机构删除', 'org-delete', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 3);
INSERT INTO `sys_menu` VALUES (653689402680709, 653335112912901, '2025-03-12 13:29:01.481851', 653335112912901, '2025-03-15 01:30:38.237695', 653688386045317, '[0][653342080185029][653688386045317]', '机构查询', 'org-search', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 4);
INSERT INTO `sys_menu` VALUES (653689923114373, 653335112912901, '2025-03-12 13:31:08.537851', 653335112912901, '2025-03-15 01:34:27.281727', 653342584296133, '[0][653342080185029][653342584296133]', '菜单查询', 'menu-search', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 4);
INSERT INTO `sys_menu` VALUES (653745302407109, 653335112912901, '2025-03-12 17:16:29.199235', 653335112912901, '2025-03-12 17:18:47.314183', 653342080185029, '[0][653342080185029]', '用户管理', '', 'User', 'user', 'MENU', 'system/user/index', 1, '', 'el-icon-Avatar', 1, 0, '', 3);
INSERT INTO `sys_menu` VALUES (653745429395397, 653335112912901, '2025-03-12 17:16:59.877507', 653335112912901, '2025-03-12 17:16:59.877621', 653745302407109, '[0][653342080185029][653745302407109]', '用户新增', 'user-create', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (653745520240581, 653335112912901, '2025-03-12 17:17:22.056285', 653335112912901, '2025-03-12 17:17:22.056480', 653745302407109, '[0][653342080185029][653745302407109]', '用户修改', 'user-update', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 2);
INSERT INTO `sys_menu` VALUES (653756020922437, 653335112912901, '2025-03-12 18:00:05.906431', 653335112912901, '2025-03-15 01:32:11.120967', 653745302407109, '[0][653342080185029][653745302407109]', '用户查询', 'user-search', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 4);
INSERT INTO `sys_menu` VALUES (653787841784005, 653335112912901, '2025-03-12 20:09:34.640689', 653335112912901, '2025-03-12 20:09:34.644133', 653745302407109, '[0][653342080185029][653745302407109]', '重置密码', 'user-reset-password', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 5);
INSERT INTO `sys_menu` VALUES (653804678302149, 653335112912901, '2025-03-12 21:18:05.045862', 653335112912901, '2025-03-12 21:18:05.050174', 653745302407109, '[0][653342080185029][653745302407109]', '用户删除', 'user-delete', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 6);
INSERT INTO `sys_menu` VALUES (653805800937925, 653335112912901, '2025-03-12 21:22:39.031803', 653335112912901, '2025-03-12 21:22:39.031846', 653745302407109, '[0][653342080185029][653745302407109]', '用户导入', 'user-import', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 7);
INSERT INTO `sys_menu` VALUES (653805917993413, 653335112912901, '2025-03-12 21:23:07.610400', 653335112912901, '2025-03-12 21:23:07.610542', 653745302407109, '[0][653342080185029][653745302407109]', '用户导出', 'user-export', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 8);
INSERT INTO `sys_menu` VALUES (654225897013253, 653335112912901, '2025-03-14 01:52:01.907929', 653335112912901, '2025-03-14 01:53:03.674597', 653342080185029, '[0][653342080185029]', '字典管理', '', 'Dict', 'dict', 'MENU', 'system/dict/index', 1, '', 'el-icon-Discount', 1, 0, '', 5);
INSERT INTO `sys_menu` VALUES (654226073489413, 653335112912901, '2025-03-14 01:52:44.618434', 653335112912901, '2025-03-14 01:52:44.618516', 654225897013253, '[0][654225897013253]', '字典新增', 'dict-create', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (654226273665029, 653335112912901, '2025-03-14 01:53:33.486091', 653335112912901, '2025-03-14 01:54:08.179769', 654225897013253, '[0][653342080185029][654225897013253]', '字典修改', 'dict-update', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 2);
INSERT INTO `sys_menu` VALUES (654226371764229, 653335112912901, '2025-03-14 01:53:57.431053', 653335112912901, '2025-03-14 01:53:57.431184', 654225897013253, '[0][653342080185029][654225897013253]', '字典删除', 'dict-delete', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 3);
INSERT INTO `sys_menu` VALUES (654226547773445, 653335112912901, '2025-03-14 01:54:40.402966', 653335112912901, '2025-03-15 01:34:59.384276', 654225897013253, '[0][653342080185029][654225897013253]', '字典查询', 'dict-search', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 4);
INSERT INTO `sys_menu` VALUES (654227048943621, 653335112912901, '2025-03-14 01:56:42.757506', 653335112912901, '2025-03-14 03:00:46.258079', 653342080185029, '[0][653342080185029]', '字典数据', '', 'DictData', 'dict-data', 'MENU', 'system/dict/data', 0, '', 'el-icon-CollectionTag', 1, 0, '', 6);
INSERT INTO `sys_menu` VALUES (654227246395397, 653335112912901, '2025-03-14 01:57:30.967744', 653335112912901, '2025-03-14 01:57:30.967803', 654227048943621, '[0][653342080185029][654227048943621]', '字典数据新增', 'dictdata-create', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (654227348602885, 653335112912901, '2025-03-14 01:57:55.923311', 653335112912901, '2025-03-14 01:57:55.923356', 654227048943621, '[0][653342080185029][654227048943621]', '字典数据修改', 'dictdata-update', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 2);
INSERT INTO `sys_menu` VALUES (654227455537157, 653335112912901, '2025-03-14 01:58:22.024924', 653335112912901, '2025-03-14 01:58:22.024962', 654227048943621, '[0][653342080185029][654227048943621]', '字典数据删除', 'dictdata-delete', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 3);
INSERT INTO `sys_menu` VALUES (654227565006853, 653335112912901, '2025-03-14 01:58:48.754711', 653335112912901, '2025-03-15 01:35:45.770369', 654227048943621, '[0][653342080185029][654227048943621]', '字典数据查询', 'dictdata-search', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 4);
INSERT INTO `sys_menu` VALUES (654332804104837, 653335112912901, '2025-03-14 09:07:02.281041', 653335112912901, '2026-09-30 17:04:24.622777', 814648632751365, '[0][653342080185029][814648632751365]', '系统配置新增', 'sysconfig-create', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (654332952756869, 653335112912901, '2025-03-14 09:07:38.205504', 653335112912901, '2026-09-30 17:04:24.622777', 814648632751365, '[0][653342080185029][814648632751365]', '系统配置修改', 'sysconfig-update', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 2);
INSERT INTO `sys_menu` VALUES (654333157950085, 653335112912901, '2025-03-14 09:08:28.289495', 653335112912901, '2026-09-30 17:04:24.622777', 814648632751365, '[0][653342080185029][814648632751365]', '系统配置删除', 'sysconfig-delete', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 3);
INSERT INTO `sys_menu` VALUES (654333310177925, 653335112912901, '2025-03-14 09:09:05.451614', 653335112912901, '2026-09-30 17:04:24.622777', 814648632751365, '[0][653342080185029][814648632751365]', '系统配置查询', 'sysconfig-search', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 4);
INSERT INTO `sys_menu` VALUES (654442132821701, 653337659433029, '2025-03-14 16:31:53.539063', 1000000000000, '2026-07-14 10:48:45.454335', 0, '[0]', '运维管理', '', '', '/maint', 'CATALOG', 'Layout', 0, '', 'el-icon-Opportunity', 1, 1, '', 11);
INSERT INTO `sys_menu` VALUES (654442722764485, 653337659433029, '2025-03-14 16:34:17.507347', 653335112912901, '2025-03-23 19:31:39.244337', 654442132821701, '[0][654442132821701]', '操作日志', '', 'OperateLog', 'operatelog', 'MENU', 'maint/log/index', 1, '', 'el-icon-Search', 1, 0, '', 4);
INSERT INTO `sys_menu` VALUES (654455068768133, 653335112912901, '2025-03-14 17:24:32.167647', 653335112912901, '2025-03-15 01:36:42.106666', 654442722764485, '[0][654442132821701][654442722764485]', '操作日志查询', 'operationlog-search', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (654523403131077, 653335112912901, '2025-03-14 22:02:35.101176', 653335112912901, '2025-03-23 19:31:34.280401', 654442132821701, '[0][654442132821701]', '登录日志', '', 'LoginLog', 'loginlog', 'MENU', 'maint/log/loginlog', 1, '', 'el-icon-ChatLineRound', 1, 0, '', 3);
INSERT INTO `sys_menu` VALUES (654523795179717, 653335112912901, '2025-03-14 22:04:10.598917', 653335112912901, '2025-03-15 01:37:04.449633', 654523403131077, '[0][654442132821701][654523403131077]', '登录日志查询', 'loginlog-search', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (654788688007877, 653335112912901, '2025-03-15 16:02:03.031386', 653335112912901, '2025-03-15 16:02:03.039044', 653525628846981, '[0][653342080185029][653525628846981]', '角色权限设置', 'role-setperms', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 5);
INSERT INTO `sys_menu` VALUES (657668727089093, 653335112912901, '2025-03-23 19:20:56.104578', 653335112912901, '2025-03-23 19:31:28.031597', 654442132821701, '[0][654442132821701]', '客户中心CAP', '', 'CustCapDashboard', 'eventbus-cust-dashboard', 'MENU', 'maint/eventbus/cust-cap-dashboard', 1, '', 'el-icon-Ship', 1, 0, '', 2);
INSERT INTO `sys_menu` VALUES (657670470342597, 653335112912901, '2025-03-23 19:28:01.718644', 653335112912901, '2025-03-23 19:31:45.945099', 654442132821701, '[0][654442132821701]', 'Loki日志', '', '', 'http://62.234.187.128:9010', 'EXTLINK', '', 1, '', 'el-icon-Cherry', 1, 0, '', 8);
INSERT INTO `sys_menu` VALUES (657670977337285, 653335112912901, '2025-03-23 19:30:05.477607', 653335112912901, '2025-03-23 19:30:05.477628', 654442132821701, '[0][654442132821701]', '容器管理', '', '', 'http://62.234.187.128:9000', 'EXTLINK', '', 1, '', 'el-icon-Compass', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (920442132822001, 0, '2026-09-30 17:03:26.653758', 910000000000501, '2026-10-13 14:42:20.256200', 0, '[0]', '智能助手', '', 'AgentCenter', '/agent', 'CATALOG', 'Layout', 1, 'agent-chat', 'visitor', 0, 1, '', 48);
INSERT INTO `sys_menu` VALUES (920442132822011, 0, '2026-09-30 17:03:26.655688', 0, '2026-09-30 17:03:26.655688', 920442132822001, '[0][920442132822001]', '智能对话', 'agent-chat', 'AgentChat', 'agent-chat', 'MENU', 'agent/chat/index', 1, '', 'chat-line-round', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (920442132822021, 0, '2026-09-30 17:03:26.657532', 0, '2026-09-30 17:03:26.657532', 920442132822001, '[0][920442132822001]', '知识库', 'agent-knowledge', 'AgentKnowledge', 'agent-knowledge', 'MENU', 'agent/knowledge/index', 1, '', 'notebook', 1, 0, '', 2);
INSERT INTO `sys_menu` VALUES (920442132822022, 0, '2026-09-30 17:03:26.659454', 0, '2026-09-30 17:03:26.659454', 920442132822021, '[0][920442132822001][920442132822021]', '上传知识', 'agent-knowledge-upload', '', '', 'BUTTON', '', 1, '', '', 0, 0, '', 1);
INSERT INTO `sys_menu` VALUES (920442132822023, 0, '2026-09-30 17:03:26.659454', 0, '2026-09-30 17:03:26.659454', 920442132822021, '[0][920442132822001][920442132822021]', '删除知识', 'agent-knowledge-delete', '', '', 'BUTTON', '', 1, '', '', 0, 0, '', 2);
INSERT INTO `sys_menu` VALUES (920442132822031, 0, '2026-09-30 17:03:26.663517', 0, '2026-09-30 17:03:26.663517', 920442132822001, '[0][920442132822001]', '会话审计', 'agent-audit', 'AgentAudit', 'agent-audit', 'MENU', 'agent/audit/index', 1, '', 'document', 1, 0, '', 3);
INSERT INTO `sys_menu` VALUES (920442132822041, 0, '2026-09-30 17:03:26.665520', 0, '2026-09-30 17:03:26.665520', 920442132822001, '[0][920442132822001]', 'LLM 运行时', 'agent-llm', 'AgentLlmRuntime', 'agent-llm', 'MENU', 'agent/llm/index', 1, '', 'cpu', 1, 0, '', 4);
INSERT INTO `sys_menu` VALUES (920442132822042, 0, '2026-09-30 17:03:26.667556', 0, '2026-09-30 17:03:26.667556', 920442132822041, '[0][920442132822001][920442132822041]', '切换模型', 'agent-llm-manage', '', '', 'BUTTON', '', 1, '', '', 0, 0, '', 1);
INSERT INTO `sys_menu` VALUES (921000000000001, 1000000000000, '2026-10-05 17:12:44.324061', 1000000000000, '2026-10-05 17:12:44.324061', 653342080185029, '[0][653342080185029]', '岗位管理', '', 'Position', 'position', 'MENU', 'system/position/index', 1, '', 'el-icon-Postcard', 1, 0, '', 6);
INSERT INTO `sys_menu` VALUES (921000000000002, 1000000000000, '2026-10-05 17:12:44.327661', 1000000000000, '2026-10-05 17:12:44.327661', 921000000000001, '[0][653342080185029][921000000000001]', '岗位新增', 'position-create', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 1);
INSERT INTO `sys_menu` VALUES (921000000000003, 1000000000000, '2026-10-05 17:12:44.329739', 1000000000000, '2026-10-05 17:12:44.329739', 921000000000001, '[0][653342080185029][921000000000001]', '岗位修改', 'position-update', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 2);
INSERT INTO `sys_menu` VALUES (921000000000004, 1000000000000, '2026-10-05 17:12:44.331695', 1000000000000, '2026-10-05 17:12:44.331695', 921000000000001, '[0][653342080185029][921000000000001]', '岗位删除', 'position-delete', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 3);
INSERT INTO `sys_menu` VALUES (921000000000005, 1000000000000, '2026-10-05 17:12:44.333852', 1000000000000, '2026-10-05 17:12:44.333852', 921000000000001, '[0][653342080185029][921000000000001]', '岗位查询', 'position-search', '', '', 'BUTTON', '', 1, '', '', 1, 0, '', 4);

-- ----------------------------
-- Table structure for sys_organization
-- ----------------------------
DROP TABLE IF EXISTS `sys_organization`;
CREATE TABLE `sys_organization`  (
  `id` bigint NOT NULL,
  `createby` bigint NOT NULL,
  `createtime` datetime(6) NOT NULL,
  `modifyby` bigint NOT NULL,
  `modifytime` datetime(6) NOT NULL,
  `parentid` bigint NOT NULL,
  `parentids` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `code` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `status` tinyint(1) NOT NULL,
  `ordinal` int NOT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'Department' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_organization
-- ----------------------------
INSERT INTO `sys_organization` VALUES (653725132137989, 653335112912901, '2025-03-12 15:54:24.703512', 653335112912901, '2025-03-12 15:54:24.709316', 0, '[0]', 'microsoft', '微软中国有限公司', 1, 1);
INSERT INTO `sys_organization` VALUES (653725132137990, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'STRATEGY', '战略办', 1, 1);
INSERT INTO `sys_organization` VALUES (653725132137991, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'MARKETING', '市场中心', 1, 2);
INSERT INTO `sys_organization` VALUES (653725132137992, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'BUSINESS', '业务中心', 1, 3);
INSERT INTO `sys_organization` VALUES (653725132137993, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'TECH', '技术部', 1, 4);
INSERT INTO `sys_organization` VALUES (653725132137994, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'HR', '人力资源中心', 1, 5);
INSERT INTO `sys_organization` VALUES (653725132137995, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'FINANCE', '财务风控中心', 1, 6);
INSERT INTO `sys_organization` VALUES (653725132137996, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'SUPPLY', '供应链中心', 1, 7);
INSERT INTO `sys_organization` VALUES (653725132137997, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'AFTERSALES', '物流售后部', 1, 8);
INSERT INTO `sys_organization` VALUES (653725132137998, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'WAREHOUSE', '仓储中心', 1, 9);
INSERT INTO `sys_organization` VALUES (653725132137999, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'OUTSOURCE', '外协人员', 1, 10);
INSERT INTO `sys_organization` VALUES (653725132138000, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'BRAZIL', '巴西群', 1, 11);
INSERT INTO `sys_organization` VALUES (653725132138001, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'LOGISTICS', '后勤部门', 1, 12);
INSERT INTO `sys_organization` VALUES (653725132138002, 1000000000000, '2026-10-06 15:11:53.000000', 1000000000000, '2026-10-06 15:11:53.000000', 653725132137989, '[0],[653725132137989]', 'VIP', 'VIP', 1, 13);

-- ----------------------------
-- Table structure for sys_position
-- ----------------------------
DROP TABLE IF EXISTS `sys_position`;
CREATE TABLE `sys_position`  (
  `id` bigint NOT NULL,
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `modifyby` bigint NOT NULL DEFAULT 0,
  `modifytime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  `code` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '岗位编码，如 DEPT_MANAGER',
  `name` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '岗位名称',
  `level` int NOT NULL DEFAULT 0 COMMENT '职级，只用于比较高低',
  `status` tinyint(1) NOT NULL DEFAULT 1 COMMENT '启用',
  `ordinal` int NULL DEFAULT 0,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_sys_position_code`(`code` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '岗位目录' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_position
-- ----------------------------
INSERT INTO `sys_position` VALUES (856511566751877, 1000000000000, '2026-10-06 16:14:31.309905', 1000000000000, '2026-10-06 16:14:31.311415', 'Role01', '总经理助理', 30, 1, NULL);
INSERT INTO `sys_position` VALUES (920000000002001, 1000000000000, '2026-10-06 15:14:14.000000', 1000000000000, '2026-10-06 15:14:14.000000', 'MANAGER', '部门经理', 20, 1, 10);
INSERT INTO `sys_position` VALUES (920000000002002, 1000000000000, '2026-10-06 15:14:14.000000', 1000000000000, '2026-10-06 15:14:14.000000', 'STAFF', '普通员工', 10, 1, 20);

-- ----------------------------
-- Table structure for sys_role
-- ----------------------------
DROP TABLE IF EXISTS `sys_role`;
CREATE TABLE `sys_role`  (
  `id` bigint NOT NULL,
  `createby` bigint NOT NULL,
  `createtime` datetime(6) NOT NULL,
  `modifyby` bigint NOT NULL,
  `modifytime` datetime(6) NOT NULL,
  `name` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `code` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `datascope` int NOT NULL,
  `status` tinyint(1) NOT NULL,
  `ordinal` int NOT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'Role' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_role
-- ----------------------------
INSERT INTO `sys_role` VALUES (653344679641925, 1000000000000, '2025-03-11 14:06:20.618864', 653335112912901, '2025-03-12 11:45:34.554147', '系统管理员', 'administrator', 0, 1, 1);
INSERT INTO `sys_role` VALUES (653682250118405, 653335112912901, '2025-03-12 12:59:55.116604', 653335112912901, '2025-03-24 23:57:04.926460', '访问者', 'guest', 2, 1, 1);
INSERT INTO `sys_role` VALUES (856513236334725, 1000000000000, '2026-10-06 16:21:18.859289', 1000000000000, '2026-10-06 16:24:36.339706', '总经理助理', 'HR002', 1, 1, 1);

-- ----------------------------
-- Table structure for sys_role_menu_relation
-- ----------------------------
DROP TABLE IF EXISTS `sys_role_menu_relation`;
CREATE TABLE `sys_role_menu_relation`  (
  `id` bigint NOT NULL,
  `menuid` bigint NOT NULL,
  `roleid` bigint NOT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'Menu-role relation' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_role_menu_relation
-- ----------------------------
INSERT INTO `sys_role_menu_relation` VALUES (658096326596997, 653342080185029, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326596998, 653688386045317, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326596999, 653689402680709, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597000, 653525628846981, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597001, 653526703580037, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597002, 653745302407109, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597003, 653756020922437, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597004, 653342584296133, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597005, 653689923114373, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597006, 654225897013253, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597007, 654226547773445, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597008, 654227048943621, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597009, 654227565006853, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597011, 654333310177925, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597012, 654442132821701, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597013, 657670977337285, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597014, 657668727089093, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597015, 654523403131077, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597016, 654523795179717, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597017, 654442722764485, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597018, 654455068768133, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (658096326597019, 657670470342597, 653682250118405);
INSERT INTO `sys_role_menu_relation` VALUES (856482891216804, 653342080185029, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891216805, 653688386045317, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891216806, 653688962147717, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891216807, 653689048724869, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891216808, 653689299236229, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891216809, 653689402680709, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891216810, 653525628846981, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891216811, 653525880882053, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220869, 653525982651269, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220870, 653526164857733, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220871, 653526703580037, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220872, 654788688007877, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220873, 653745302407109, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220874, 653745429395397, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220875, 653745520240581, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220876, 653756020922437, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220877, 653787841784005, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220878, 653804678302149, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220879, 653805800937925, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220880, 653805917993413, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220881, 653342584296133, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220882, 653343418946245, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220883, 653389651534725, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220884, 653524636169093, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220885, 653689923114373, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220886, 654225897013253, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220887, 654226073489413, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220888, 654226273665029, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220889, 654226371764229, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220890, 654226547773445, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220891, 654227048943621, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220892, 654227246395397, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220893, 654227348602885, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220894, 654227455537157, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220895, 654227565006853, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220896, 921000000000001, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220897, 921000000000002, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220898, 921000000000003, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220899, 921000000000004, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220900, 921000000000005, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220901, 814648632751365, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220902, 654332804104837, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220903, 654332952756869, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220904, 654333157950085, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220905, 654333310177925, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220906, 654442132821701, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220907, 657670977337285, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220908, 657668727089093, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220909, 654523403131077, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220910, 654523795179717, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220911, 654442722764485, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220912, 654455068768133, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891220913, 657670470342597, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891224997, 920442132822001, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891224998, 920442132822011, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891224999, 920442132822021, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891225000, 920442132822022, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891225001, 920442132822023, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891225002, 920442132822031, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891225003, 920442132822041, 653344679641925);
INSERT INTO `sys_role_menu_relation` VALUES (856482891225004, 920442132822042, 653344679641925);

-- ----------------------------
-- Table structure for sys_role_user_relation
-- ----------------------------
DROP TABLE IF EXISTS `sys_role_user_relation`;
CREATE TABLE `sys_role_user_relation`  (
  `id` bigint NOT NULL,
  `userid` bigint NOT NULL,
  `roleid` bigint NOT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'User-role relation' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_role_user_relation
-- ----------------------------
INSERT INTO `sys_role_user_relation` VALUES (856483442190213, 910000000000501, 653344679641925);

-- ----------------------------
-- Table structure for sys_user
-- ----------------------------
DROP TABLE IF EXISTS `sys_user`;
CREATE TABLE `sys_user`  (
  `id` bigint NOT NULL,
  `isdeleted` tinyint(1) NOT NULL DEFAULT 0 COMMENT 'Deletion flag',
  `createby` bigint NOT NULL,
  `createtime` datetime(6) NOT NULL,
  `modifyby` bigint NOT NULL,
  `modifytime` datetime(6) NOT NULL,
  `account` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Account',
  `avatar` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Avatar path',
  `birthday` datetime(6) NULL DEFAULT NULL COMMENT 'Birthday',
  `deptid` bigint NOT NULL COMMENT '主职部门（冗余，方便列表） → sys_organization.id',
  `email` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'email',
  `name` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Name',
  `password` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Password',
  `mobile` varchar(11) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Mobile number',
  `salt` varchar(6) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Password salt',
  `gender` int NOT NULL COMMENT 'Gender',
  `status` tinyint(1) NOT NULL COMMENT 'Status',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'Administrator' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_user
-- ----------------------------
INSERT INTO `sys_user` VALUES (910000000000501, 0, 1000000000000, '2026-09-30 17:04:15.219936', 1000000000000, '2026-10-05 09:46:38.739016', 'admin', '', NULL, 653726253618757, 'admin@nx.local', '管理员', '6AC4FD03ADC43CBFFB66750009180981', '', 'nxadm1', 0, 1);

-- ----------------------------
-- Table structure for sys_user_appointment
-- ----------------------------
DROP TABLE IF EXISTS `sys_user_appointment`;
CREATE TABLE `sys_user_appointment`  (
  `id` bigint NOT NULL,
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `modifyby` bigint NOT NULL DEFAULT 0,
  `modifytime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  `userid` bigint NOT NULL COMMENT '→ sys_user.id',
  `deptid` bigint NOT NULL COMMENT '→ sys_organization.id',
  `positionid` bigint NOT NULL COMMENT '→ sys_position.id',
  `isprimary` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否主职',
  `status` tinyint(1) NOT NULL DEFAULT 1 COMMENT '有效',
  `effectivestart` datetime(6) NULL DEFAULT NULL COMMENT '生效开始，空表示不限',
  `effectiveend` datetime(6) NULL DEFAULT NULL COMMENT '生效结束，空表示不限',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_sys_user_appointment`(`userid` ASC, `deptid` ASC, `positionid` ASC) USING BTREE,
  INDEX `ix_sys_user_appointment_user`(`userid` ASC, `status` ASC, `isprimary` ASC) USING BTREE,
  INDEX `ix_sys_user_appointment_dept_pos`(`deptid` ASC, `positionid` ASC, `status` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '用户任职' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of sys_user_appointment
-- ----------------------------

SET FOREIGN_KEY_CHECKS = 1;
