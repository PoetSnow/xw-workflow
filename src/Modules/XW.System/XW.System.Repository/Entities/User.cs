using XW.Infra.Repository;

namespace XW.System.Repository.Entities
{
    /// <summary>
    /// 系统用户
    /// </summary>
    public class User : EfFullAuditEntity, ISoftDelete
    {
        /// <summary>Account最大长度。</summary>
        public static readonly int Account_MaxLength = 32;
        /// <summary>Avatar最大长度。</summary>
        public static readonly int Avatar_MaxLength = 128;
        /// <summary>Email_Maxlength。</summary>
        public static readonly int Email_Maxlength = 32;
        /// <summary>Name_Maxlength。</summary>
        public static readonly int Name_Maxlength = 32;
        /// <summary>Password_Maxlength。</summary>
        public static readonly int Password_Maxlength = 32;
        /// <summary>Phone_Maxlength。</summary>
        public static readonly int Phone_Maxlength = 11;
        /// <summary>Role Ids_Maxlength。</summary>
        public static readonly int RoleIds_Maxlength = 72;
        /// <summary>Salt_Maxlength。</summary>
        public static readonly int Salt_Maxlength = 6;

        //private SysDept _dept;
        //private Action<object, string> LazyLoader { get; set; }
        //private SysUser(Action<object, string> lazyLoader)
        //{
        //	LazyLoader = lazyLoader;
        //}
        //public virtual SysDept Org
        //{
        //   get => LazyLoader.Load(this, ref _dept);
        //   set => _dept = value;
        //}

        /// <summary>
        /// Account
        /// </summary>
        public string Account { get; set; } = string.Empty;

        /// <summary>
        /// Avatar path
        /// </summary>
        public string Avatar { get; set; } = string.Empty;

        /// <summary>
        /// Birthday
        /// </summary>
        public DateTime? Birthday { get; set; }

        /// <summary>
        /// Department ID
        /// </summary>
        public long DeptId { get; set; }

        /// <summary>
        /// email
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Password
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Mobile number
        /// </summary>
        public string Mobile { get; set; } = string.Empty;

        /// <summary>
        /// Password salt
        /// </summary>
        public string Salt { get; set; } = string.Empty;

        /// <summary>
        /// Gender
        /// </summary>
        public int Gender { get; set; }

        /// <summary>
        /// Status
        /// </summary>
        public bool Status { get; set; }

        /// <summary>
        /// Deletion flag
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
