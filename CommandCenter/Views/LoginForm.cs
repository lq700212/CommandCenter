using System;
using System.Windows.Forms;
using CommandCenter.Models;
using CommandCenter.Utils;

namespace CommandCenter.Views
{
    /// <summary>
    /// 管理员登录对话框（V1.9.0）：既是登录入口，也是改密码入口，账号管理都在这里完成，
    /// 不占用系统设置窗体的空间。
    ///
    /// 【界面布局】
    /// ┌────────────────────────────────────────────┐
    /// │          ▓ 管理员登录（横幅）▓               │
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
    ///     加密存到 %LOCALAPPDATA%\CommandCenter\remembered_login.dat，下次打开登录框
    ///     自动回填（记录用户名必须与当前管理员账号一致才回填）；取消勾选则清掉旧记录；
    ///   - 修改密码：需先验证【原密码】（即当前密码），新密码两次一致且 ≥6 位才保存；
    ///     保存后写盘 appconfig.json（ConfigStore.Save）并即时生效，下次登录用新密码；
    ///     同时若勾选了"记住密码"会把记住文件同步成新密码，否则清掉；
    ///   - 密码只存 SHA-256 哈希（SecurityUtil.HashPassword），配置里看不到明文。
    /// </summary>
    public partial class LoginForm : Form
    {
        private readonly AppConfig _config; // 持有整个配置：登录比对 + 改密码写盘都用它（改的是同一实例）

        public LoginForm(AppConfig config)
        {
            _config = config ?? new AppConfig();
            InitializeComponent();
            ShowLoginPanel(null, EventArgs.Empty); // 默认显示登录面板

            // 账号默认填管理员账号（通常 "admin"），现场就一个管理员，免输入；
            // 若勾过"记住密码"则下面回填记录里的用户名/密码，覆盖这个默认值。
            txtUser.Text = _config.Security.AdminUser ?? "admin";

            // 读取"记住密码"记录：勾选过的用户名+密码自动回填（DPAPI 解密，见 SecurityUtil）。
            // 只有记录里的用户名与当前配置的管理员账号一致才回填，防止换账号后残留旧密码。
            string savedUser, savedPwd;
            if (SecurityUtil.LoadRememberedLogin(out savedUser, out savedPwd)
                && string.Equals(savedUser, _config.Security.AdminUser, StringComparison.OrdinalIgnoreCase))
            {
                txtUser.Text = savedUser;
                txtPwd.Text = savedPwd;
                chkRemember.Checked = true; // 回填了就把勾选框也带上，用户能一眼看出"记住了"
            }

            txtUser.Focus();
            txtUser.SelectAll(); // 选中整个账号文本，用户若想改直接输入即覆盖
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
            lblBanner.Text = "管理员登录";
            AcceptButton = btnLogin;
        }

        /// <summary>切到修改密码面板：隐藏登录面板，回车=保存，焦点给原密码框。</summary>
        private void ShowChangePwdPanel(object sender, EventArgs e)
        {
            pnlLogin.Visible = false;
            pnlChangePwd.Visible = true;
            pnlChangePwd.BringToFront();
            lblBanner.Text = "修改密码";
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
        /// </summary>
        private void BtnLogin_Click(object sender, EventArgs e)
        {
            string user = txtUser.Text.Trim();
            string pwdHash = SecurityUtil.HashPassword(txtPwd.Text);

            bool userOk = string.Equals(user, _config.Security.AdminUser,
                StringComparison.OrdinalIgnoreCase);
            bool pwdOk = !string.IsNullOrEmpty(pwdHash)
                && string.Equals(pwdHash, _config.Security.AdminPasswordHash,
                    StringComparison.OrdinalIgnoreCase);

            if (userOk && pwdOk)
            {
                LogHelper.Info("管理员登录成功：" + _config.Security.AdminUser);
                // 记住密码：勾选则 DPAPI 加密保存（下次自动回填），未勾选则清掉旧记录。
                // 放在校验通过后才写，避免错误密码污染记住文件。
                if (chkRemember.Checked)
                    SecurityUtil.SaveRememberedLogin(user, txtPwd.Text);
                else
                    SecurityUtil.ClearRememberedLogin();
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            LogHelper.Warn($"管理员登录失败（用户名={user}）");
            MessageBox.Show("用户名或密码错误，请重新输入。", "登录失败",
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
                MessageBox.Show("原密码不正确，请重新输入。", "修改密码",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtOldPwd.Clear();
                txtOldPwd.Focus();
                return;
            }

            string newPwd = txtNewPwd.Text;
            if (newPwd.Length < 6)
            {
                MessageBox.Show("新密码至少 6 位，请重新输入。", "修改密码",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPwd.Clear();
                txtNewPwd.Focus();
                return;
            }
            if (newPwd != txtNewPwd2.Text)
            {
                MessageBox.Show("两次输入的新密码不一致，请重新输入。", "修改密码",
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
            if (chkRemember.Checked)
                SecurityUtil.SaveRememberedLogin(_config.Security.AdminUser, newPwd);
            else
                SecurityUtil.ClearRememberedLogin();

            try
            {
                ConfigStore.Save(_config);
                LogHelper.Info("管理员密码已修改并写盘");
                MessageBox.Show("密码修改成功，下次登录请使用新密码。", "修改密码",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                ShowLoginPanel(null, EventArgs.Empty); // 改完切回登录面板，用新密码登录即可
                txtPwd.Clear();
                txtPwd.Focus();
            }
            catch (Exception ex)
            {
                LogHelper.Error("保存管理员密码失败：" + ex.Message);
                MessageBox.Show("密码已修改但写盘失败（" + ex.Message + "），本次运行生效，重启后可能恢复旧密码。",
                    "修改密码", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
