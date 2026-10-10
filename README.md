# wargame

Unity 战争游戏项目。当前仓库提供 Unity 工程源码，直接运行的游戏安装包需单独构建和发布。

## 打开项目

1. 安装 Unity Hub 和 Unity **2022.3.62f3c1**。
2. 下载仓库 ZIP 并解压，或使用 Git 克隆仓库。
3. 在 Unity Hub 中添加包含 `Assets`、`Packages`、`ProjectSettings` 的目录。
4. 等待 Unity 导入资源，打开 `Assets/Scenes/Battle01.unity`，点击 Play。

具体玩法和配置请阅读根目录的 `wargame完整使用说明书.txt` 及其他说明文件。

## 协作

- 仓库拥有者在 GitHub 的 Settings → Collaborators 中邀请协作者。
- 每项修改使用独立分支，完成后发起 Pull Request，由另一人检查后合并。
- 提交资源时同时提交对应 `.meta` 文件，保留现有 `.meta`，避免资源引用丢失。
- 避免多人同时修改同一个场景或预制体；开始修改前先约定负责范围。
- 不提交 Library、Temp、Logs、UserSettings 或本机密钥。
- 建议在 Unity 中使用 Visible Meta Files 和 Force Text，便于版本管理。

## 游戏下载

源码下载：仓库页面 Code → Download ZIP。

可直接运行的版本：由维护者通过 Unity 的 Build Settings 构建后，将完整构建目录压缩并上传 GitHub Releases。Windows 版本需包含可执行文件及其配套数据文件。

## 授权

本项目暂未指定开源许可证。公开仓库用于查看和协作；其他用途的授权请联系项目拥有者。
