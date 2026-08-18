using System;
using System.Windows.Forms;
using CommandCenter.Models;
using CommandCenter.Utils;

namespace CommandCenter.Views
{
    /// <summary>
    /// 登录角色（V1.12.0）：LoginForm 校验通过后置入 Role 属性，
    /// MainForm.OpenSettings 据此决定打开哪个界面：
    ///   Admin → 系统设置窗体 SettingsForm（改配置，权限最高）；
    ///   Developer → 功能测试窗体 DevTestForm（相机/PLC 通讯验证，不碰业务配置）。
    /// </summary>
    public enum LoginRole
    {
        /// <summary>未登录/取消（默认值，调用方应忽略）</summary>
        None,

        /// <summary>管理员账号：进系统设置</summary>
        Admin,

        /// <summary>开发者账号：进功能测试</summary>
        Developer
    }

    /// <summary>
    /// 管理员登录对话框（V1.9.0）：既是登录入口，也是改密码入口，账号管理都在这里完成，
    /// 不占用系统设置窗体的空间。
    ///
    /// 【V1.12.0 双账号】新增开发者账号（SecurityConfig.DevUser，默认 dev/dev123）：
    ///   登录后通过 Role 属性告知调用方角色，MainForm 据此分流：
    ///   - 管理员 → 打开系统设置窗体（原有行为）；
    ///   - 开发者 → 打开功能测试窗体 DevTestForm（PLC/相机通讯验证，不碰业务配置）。
    ///   登录框标题/横幅跟随当前面板语义，不再写死"管理员"三个字。
    ///
    /// 【界面布局】
    /// ┌────────────────────────────────────────────┐
    /// │          ▓ 账号登录（横幅）▓                 │
    /// ├────────────────────────────────────────────┤
    /// │  用户名:  [txtUser（默认已填 admin）]        │
    /// │  密　码:  [txtPwd               ]          │
    /// │  [x]记住密码                               │
    /// │  [修改密码]                  [登 录]        │
    /// ├────────────────────────────────────────────┤
    /// │          ▓ 修改密码（横幅）▓                │
    /// ├────────────────────────────────────────────┤
    /// │  原密码:  [txtOldPwd            ]          │
    /// │  新密码:  [txtNewPwd            ]          │
    /// │  确认密码:[txtNewPwd2           ]          │
    /// │  提示: 至少 6 位（灰色小字）                │
    /// │  [返回登录]                  [保存修改]      │
    /// └────────────────────────────────────────────┘
    /// 两个面板（pnlLogin / pnlChangePwd）切换显示，横幅文字跟随变化。
    ///
    /// 【交互】
    ///   - 账号框默认填当前管理员账号（通常 admin），无需输入；记住密码回填会覆盖它；
    ///   - 登录：回车=登录（AcceptButton），校验通过 DialogResult=OK 关闭；
    ///   - 记住密码（chkRemember）：勾选后登录成功把"用户名+密码"用 Windows DPAPI
    ///     加密存到 %LOCALAPPDATA%\CommandCenter\（记录用户名必须与当前账号一致才回填）；
    ///     取消勾选则清掉旧记录。V1.12.21 起管理员与开发者都可记住，且记录互斥：
    ///     - 管理员记住 → 存 remembered_login.dat；登录成功会把开发者文件一并清掉；
    ///     - 开发者记住 → 存 remembered_login_dev.dat；登录成功会把管理员文件一并清掉；
    ///     - 目的：机器上只保留"最近一次登录的那个角色"的账号记忆，防止跨角色残留
    ///       （如 dev 记住了、登录框还回填 dev；admin 记住了却回填 admin 造成串号）。
    ///   - 修改密码：仅管理员账号可用（开发者密码在配置里维护，界面不支持改）；
    ///   - 密码只存 SHA-256 哈希（SecurityUtil.HashPassword），配置里看不到明文。
    /// </summary>
    public partial class LoginForm : Form
    {
        private readonly AppConfig _config; // 持有整个配置：登录比对 + 改密码写盘都用它（改的是同一实例）

        /// <summary>
        /// 登录成功后的角色（V1.12.0）：调用方 ShowDialog 返回 OK 后据此分流。
        /// 默认 None；BtnLogin_Click 校验通过时置为 Admin 或 Developer。
        /// </summary>
        public LoginRole Role { get; private set; } = LoginRole.None;

        public LoginForm(AppConfig config)
        {
            _config = config ?? new AppConfig();
            InitializeComponent();
            ShowLoginPanel(null, EventArgs.Empty); // 默认显示登录面板

            // 账号默认填管理员账号（通常 "admin"），现场就一个管理员，免输入；
            // 若勾过"记住密码"则下面回填记录里的用户名/密码，覆盖这个默认值。
            txtUser.Text = _config.Security.AdminUser ?? "admin";

            // 读取"记住密码"记录：勾选过的用户名+密码自动回填（DPAPI 解密，见 SecurityUtil）。
            // V1.12.21 起管理员/开发者可各自记住（分文件）。回填规则：
            //   管理员记住记录里的用户名==配置管理员账号 → 回填；
            //   否则看开发者记录（需 DevEnabled）用户名==配置开发者账号 → 回填；
            //   两边都不匹配（换账号/版本升级残留）则不回填，保持默认 admin。
            string savedUser, savedPwd;
            if (SecurityUtil.LoadRememberedLogin(false, out savedUser, out savedPwd)
                && string.Equals(savedUser, _config.Security.AdminUser, StringComparison.OrdinalIgnoreCase))
            {
                txtUser.Text = savedUser;
                txtPwd.Text = savedPwd;
                chkRemember.Checked = true; // 回填了就把勾选框也带上，用户能一眼看出"记住了"
            }
            else if (_config.Security.DevEnabled
                && SecurityUtil.LoadRememberedLogin(true, out savedUser, out savedPwd)
                && string.Equals(savedUser, _config.Security.DevUser, StringComparison.OrdinalIgnoreCase))
            {
                txtUser.Text = savedUser;
                txtPwd.Text = savedPwd;
                chkRemember.Checked = true; // 同上：回填开发者账号时也把勾选框带上
            }

            txtUser.Focus();
            txtUser.SelectAll(); // 选中整个账号文本，用户若想改直接输入即覆盖
            ApplyLanguage();    // V2.15.0 国际化：按当前语言初始化全部文本
        }

        /// <summary>ESC 键：改密码面板时先回登录面板；登录面板时直接关闭（同取消）。</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                if (pnlChangePwd.Visible)
                {
                    ShowLoginPanel(null, EventArgs.Empty);
                }
                else
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
                e.Handled = true;
            }
        }

        // ────────────── 面板切换 ──────────────

        /// <summary>切到登录面板：隐藏改密码面板，回车=登录，焦点给用户名框。</summary>
        private void ShowLoginPanel(object sender, EventArgs e)
        {
            pnlChangePwd.Visible = false;
            pnlLogin.Visible = true;
            pnlLogin.BringToFront();
            lblBanner.Text = I18n.T("账号登录", "Account Login"); // V2.15.0 双语
            AcceptButton = btnLogin;
        }

        /// <summary>切到修改密码面板：隐藏登录面板，回车=保存，焦点给原密码框。
        /// 【V1.12.0】仅管理员账号允许改密码；开发者账号（dev）的密码由配置维护，
        /// 若用户名输入框里填的是开发者账号，点"修改密码"直接提示并留在登录面板。</summary>
        private void ShowChangePwdPanel(object sender, EventArgs e)
        {
            // 开发者账号不支持界面改密码（其哈希在配置里维护，见 SecurityConfig.DevPasswordHash 注释）
            bool isDev = _config.Security.DevEnabled
                && string.Equals(txtUser.Text.Trim(), _config.Security.DevUser,
                    StringComparison.OrdinalIgnoreCase);
            if (isDev)
            {
                MessageBox.Show(I18n.T(
                    "开发者账号的密码在配置中维护，不支持在此修改。\n请用管理员账号修改或改配置文件。",
                    "The developer account password is maintained in the config file and cannot be changed here.\nUse the admin account or edit the config file."),
                    I18n.T("修改密码", "Change Password"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            pnlLogin.Visible = false;
            pnlChangePwd.Visible = true;
            pnlChangePwd.BringToFront();
            lblBanner.Text = I18n.T("修改密码", "Change Password"); // V2.15.0 双语
            AcceptButton = btnSavePwd;
            txtOldPwd.Clear();
            txtNewPwd.Clear();
            txtNewPwd2.Clear();
            txtOldPwd.Focus();
        }

        // ────────────── 登录 ──────────────

        /// <summary>
        /// 点"登录"（或回车）：比对用户名 + 密码，通过则 DialogResult=OK 关闭。
        /// 用户名大小写不敏感（现场不用纠结 caps 键）；密码必须精确匹配。
        /// 【V1.12.0 双账号】先比对管理员账号（_config.Security.AdminUser/AdminPasswordHash），
        /// 不匹配再比对开发者账号（DevUser/DevPasswordHash，且 DevEnabled=true 才生效）。
        /// 校验通过后按角色设置 Role 属性供调用方分流；记住密码只对管理员账号生效。
        /// </summary>
        private void BtnLogin_Click(object sender, EventArgs e)
        {
            string user = txtUser.Text.Trim();
            string pwdHash = SecurityUtil.HashPassword(txtPwd.Text);

            // 管理员账号校验（原有逻辑）
            bool isAdmin = string.Equals(user, _config.Security.AdminUser,
                StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(pwdHash)
                && string.Equals(pwdHash, _config.Security.AdminPasswordHash,
                    StringComparison.OrdinalIgnoreCase);

            // 开发者账号校验（V1.12.0）：DevEnabled 关闭时直接不认
            bool isDev = _config.Security.DevEnabled
                && string.Equals(user, _config.Security.DevUser,
                    StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(pwdHash)
                && string.Equals(pwdHash, _config.Security.DevPasswordHash,
                    StringComparison.OrdinalIgnoreCase);

            if (isAdmin)
            {
                Role = LoginRole.Admin;
                LogHelper.Info("管理员登录成功：" + _config.Security.AdminUser);
                // 记住密码：勾选则 DPAPI 加密保存（下次自动回填，admin 存 remembered_login.dat），
                // 未勾选则清掉管理员旧记录。放在校验通过后才写，避免错误密码污染记住文件。
                // V1.12.21 互斥：管理员登录成功，同时清掉开发者记住记录——避免这台机器还残留
                //   开发者的免密入口（换角色登录后只保留最近一次登录角色的记忆）。
                if (chkRemember.Checked)
                    SecurityUtil.SaveRememberedLogin(false, user, txtPwd.Text);
                else
                    SecurityUtil.ClearRememberedLogin(false);
                SecurityUtil.ClearRememberedLogin(true); // 清开发者记录（管理员登录后不再记住 dev）
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            if (isDev)
            {
                Role = LoginRole.Developer;
                LogHelper.Info("开发者登录成功：" + _config.Security.DevUser);
                // V1.12.21 开发者也可记住密码：勾选则存 remembered_login_dev.dat，
                // 未勾选则清掉开发者旧记录；同时把管理员记住文件清掉（与管理员登录互斥，
                // 防止 dev 登录后登录框仍回填管理员的免密账号，造成权限外泄）。
                if (chkRemember.Checked)
                    SecurityUtil.SaveRememberedLogin(true, user, txtPwd.Text);
                else
                    SecurityUtil.ClearRememberedLogin(true);
                SecurityUtil.ClearRememberedLogin(false); // 清管理员记录（开发者登录后不再记住 admin）
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            LogHelper.Warn($"登录失败（用户名={user}）");
            MessageBox.Show(I18n.T("用户名或密码错误，请重新输入。", "Incorrect user name or password. Please try again."),
                I18n.T("登录失败", "Login Failed"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtPwd.Clear();
            txtPwd.Focus();
        }

        // ────────────── 修改密码 ──────────────

        /// <summary>
        /// 点"保存修改"（或回车）：先验证原密码（=当前密码），再校验新密码
        /// 两次一致且 ≥6 位，全部通过则更新配置里的密码哈希并写盘。
        /// 保存后停留在改密码面板并提示，方便继续改；下次登录用新密码。
        /// </summary>
        private void BtnSavePwd_Click(object sender, EventArgs e)
        {
            string oldPwdHash = SecurityUtil.HashPassword(txtOldPwd.Text);
            if (string.IsNullOrEmpty(oldPwdHash)
                || !string.Equals(oldPwdHash, _config.Security.AdminPasswordHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(I18n.T("原密码不正确，请重新输入。", "The current password is incorrect. Please re-enter it."),
                    I18n.T("修改密码", "Change Password"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtOldPwd.Clear();
                txtOldPwd.Focus();
                return;
            }

            string newPwd = txtNewPwd.Text;
            if (newPwd.Length < 6)
            {
                MessageBox.Show(I18n.T("新密码至少 6 位，请重新输入。", "New password must be at least 6 characters. Please re-enter it."),
                    I18n.T("修改密码", "Change Password"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPwd.Clear();
                txtNewPwd.Focus();
                return;
            }
            if (newPwd != txtNewPwd2.Text)
            {
                MessageBox.Show(I18n.T("两次输入的新密码不一致，请重新输入。", "The two new passwords do not match. Please re-enter them."),
                    I18n.T("修改密码", "Change Password"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPwd2.Clear();
                txtNewPwd2.Focus();
                return;
            }

            // 更新内存配置并写盘：ConfigStore.Save 落 appconfig.json，
            // 下次登录（每次点系统设置都重新登录）即按新密码校验。
            _config.Security.AdminPasswordHash = SecurityUtil.HashPassword(newPwd);

            // 若勾了"记住密码"，把记住文件同步成新密码（否则下次回填旧密码会登录失败）；
            // 未勾选则清掉旧记录。改密码面板没有用户名输入框，这里直接用配置里的管理员账号。
            // V1.12.21：改密码属管理员操作，同样清掉开发者记住记录（保持互斥，见 BtnLogin_Click）。
            if (chkRemember.Checked)
                SecurityUtil.SaveRememberedLogin(false, _config.Security.AdminUser, newPwd);
            else
                SecurityUtil.ClearRememberedLogin(false);
            SecurityUtil.ClearRememberedLogin(true); // 管理员改密码后不再记住 dev

            try
            {
                ConfigStore.Save(_config);
                LogHelper.Info("管理员密码已修改并写盘");
                MessageBox.Show(I18n.T("密码修改成功，下次登录请使用新密码。", "Password changed. Use the new password next time you log in."),
                    I18n.T("修改密码", "Change Password"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                ShowLoginPanel(null, EventArgs.Empty); // 改完切回登录面板，用新密码登录即可
                txtPwd.Clear();
                txtPwd.Focus();
            }
            catch (Exception ex)
            {
                LogHelper.Error("保存管理员密码失败：" + ex.Message);
                MessageBox.Show(I18n.T(
                    "密码已修改但写盘失败（" + ex.Message + "），本次运行生效，重启后可能恢复旧密码。",
                    "Password changed but saving failed (" + ex.Message + "). Effective for this run; it may revert after restart."),
                    I18n.T("修改密码", "Change Password"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// V2.15.0 国际化：按当前语言刷新本窗体全部界面文字。
        /// 在构造函数末尾调用（模态对话框打开瞬间按当前语言初始化；模态期间语言不会变化）。
        /// Designer 里的静态中文文本保持原样（方便 VS 设计器维护布局），运行时统一在这里覆盖。
        /// </summary>
        private void ApplyLanguage()
        {
            this.Text = I18n.T("账号登录", "Account Login");
            lblBanner.Text = I18n.T("账号登录", "Account Login"); // 面板切换时会再按语义覆盖（登录/改密码）
            lblUser.Text = I18n.T("用户名:", "User Name:");
            lblPwd.Text = I18n.T("密　码:", "Password:");
            chkRemember.Text = I18n.T("记住密码", "Remember Password");
            btnChangePwd.Text = I18n.T("修改密码", "Change Password");
            btnLogin.Text = I18n.T("登 录", "Log In");
            lblOldPwd.Text = I18n.T("原密码:", "Current Password:");
            lblNewPwd.Text = I18n.T("新密码:", "New Password:");
            lblNewPwd2.Text = I18n.T("确认密码:", "Confirm Password:");
            btnSavePwd.Text = I18n.T("保存修改", "Save");
            btnBack.Text = I18n.T("返回登录", "Back to Login");
            lblPwdHint.Text = I18n.T("新密码至少 6 位，改后需用新密码登录", "New password must be at least 6 characters; use it next login");

            // V2.15.6：改密码面板输入框布局随语言切换——中文标签短（原密码:/新密码:/确认密码:），
            // 用宽输入框（左缘 112、宽 240）；英文标签变长（Current Password: 等最长 133px），
            // 输入框收窄并右移（左缘 170、宽 182）让左侧标题完整显示、互不重叠。
            // Designer 静态值保持中文版式（方便 VS 设计器维护），这里按语言覆盖。
            bool en = I18n.Language == "en-US";
            int pwdLeft = en ? 170 : 112;
            int pwdWidth = en ? 182 : 240;
            txtOldPwd.Location = new System.Drawing.Point(pwdLeft, txtOldPwd.Top);
            txtOldPwd.Width = pwdWidth;
            txtNewPwd.Location = new System.Drawing.Point(pwdLeft, txtNewPwd.Top);
            txtNewPwd.Width = pwdWidth;
            txtNewPwd2.Location = new System.Drawing.Point(pwdLeft, txtNewPwd2.Top);
            txtNewPwd2.Width = pwdWidth;

            // V2.15.6：改密码提示（lblPwdHint）英文文字较长（约 366px），在改密码面板内左右居中；
            // 中文提示较短，保持原左对齐（左缘 114，与输入框文字起点对齐）。
            // 注意：AutoSize 下 Width 已随上一步 lblPwdHint.Text 更新，这里按最终宽度算 Left。
            lblPwdHint.Left = en ? (pnlChangePwd.ClientSize.Width - lblPwdHint.Width) / 2 : 114;
        }
    }
}
