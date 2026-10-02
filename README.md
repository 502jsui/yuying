# yuying

`yuying` 是一个使用 Unity 制作的单机独立游戏项目。

## 开发环境

- Unity Editor：`2022.3.62f3`
- 版本控制：Git + Git LFS
- 远程仓库：<https://github.com/502jsui/yuying>

## 首次获取项目

1. 先接受仓库管理员发送的 GitHub 协作者邀请。
2. 安装 [Git LFS](https://git-lfs.com/)。
3. 在终端执行：

   ```bash
   git lfs install
   git clone https://github.com/502jsui/yuying.git
   ```

4. 在 Unity Hub 中打开克隆后的目录。
5. 使用 Unity `2022.3.62f3` 打开项目，等待包和资源导入完成。
6. 打开 `Assets/Scenes/SampleScene.unity`，确认编辑器控制台没有报错。

## 使用 GitHub Desktop 提交项目

以下流程推荐队友日常使用。请始终通过功能分支和 Pull Request 修改项目，不要直接向 `main` 提交。

### 准备工作

1. 接受 GitHub 协作者邀请。
2. 安装 [GitHub Desktop](https://desktop.github.com/) 和 [Git LFS](https://git-lfs.com/)。
3. 安装完成后重新打开 GitHub Desktop，并登录自己的 GitHub 账号。
4. 确保电脑已安装 Unity `2022.3.62f3`。如果版本不同，先通过 Unity Hub 安装对应版本。

### 第一次下载仓库

1. 在 GitHub Desktop 中选择 `File` → `Clone repository`。
2. 打开 `URL` 标签，填写：

   ```text
   https://github.com/502jsui/yuying.git
   ```

3. `Local path` 建议选择不包含中文且空间充足的目录，例如：

   ```text
   E:\UnityProjects\yuying
   ```

4. 点击 `Clone`，等待仓库和 Git LFS 素材下载完成。
5. 在 Unity Hub 中点击 `Add project from disk`，选择刚下载的 `yuying` 目录。
6. 使用 Unity `2022.3.62f3` 打开项目，并确认 `Assets/Scenes/SampleScene.unity` 可以正常加载。

### 每次开始工作前同步

1. 在 GitHub Desktop 顶部点击 `Fetch origin`。
2. 如果出现 `Pull origin` 按钮，点击它，将队友已经合并到 `main` 的内容同步到本地。
3. 切换分支、拉取或合并前，先关闭 Unity，避免资源文件和缓存被占用。
4. 同步完成后再打开 Unity 开始工作。

### 创建自己的功能分支

1. 确认 `Current branch` 是 `main`，并且已经完成同步。
2. 点击 `Current branch` → `New branch`。
3. 根据工作内容命名分支：
   - 新功能：`feat/功能名`
   - 修复问题：`fix/问题名`
   - 美术素材：`art/素材名`
   - 文档修改：`docs/文档名`
4. 点击 `Create branch`，后续修改都提交到这个分支。

### 在 Unity 中修改并提交

1. 在 Unity 中完成工作，确认控制台没有报错，并运行受影响的场景。
2. 提交前保存场景，然后关闭 Unity。
3. 回到 GitHub Desktop，在 `Changes` 中检查准备提交的文件。
4. 确认没有选择 `Library`、`Temp`、`Logs`、`UserSettings`、`Build` 或 `Builds` 等目录。
5. 在左下角填写简短的 `Summary`，例如：

   ```text
   feat: 添加角色移动
   ```

6. 需要时补充 `Description`，然后点击 `Commit to 分支名`。
7. 点击 `Push origin`，把提交上传到 GitHub。

### 创建 Pull Request

1. 分支成功推送后，在 GitHub Desktop 点击 `Create Pull Request`。
2. 确认 `base` 是 `main`，`compare` 是自己的功能分支。
3. 填写标题和修改说明，例如做了什么、测试了什么、还遗留什么问题。
4. 点击创建 Pull Request，并通知队友检查。
5. 如果队友要求修改，继续在自己的分支中提交并点击 `Push origin`；Pull Request 会自动更新。
6. Pull Request 合并后，切回 `main`，点击 `Fetch origin` 和 `Pull origin` 获取最新版本。

### 重要注意事项

- 不要直接向 `main` 提交，也不要使用 `Publish repository`；本项目已经有远程仓库。
- 不要提交 `Library`、`Temp`、`Logs`、`UserSettings`、构建产物或 IDE 临时文件。
- 图片、音频、模型、视频和字体等大文件由 Git LFS 管理；安装 Git LFS 后正常提交即可。
- 修改 `.unity`、`.prefab` 或 `.asset` 文件前尽量先同步，避免两人同时修改同一资源。
- 如果 GitHub Desktop 提示场景或预制体冲突，不要随意选择整份文件覆盖，先联系另一名队友确认修改内容。
- 不确定文件是否可以提交时，先暂停并询问仓库管理员。

## 协作流程

- `main` 是稳定分支，始终保持可以打开和运行。
- 开发前先从最新的 `main` 创建功能分支：
  - `feat/功能名`
  - `fix/问题名`
  - `art/素材名`
  - `docs/文档名`
- 完成后提交并推送功能分支，然后在 GitHub 创建 Pull Request。
- 合并前至少确认 Unity 控制台无错误，并运行受影响的场景。
- 不要提交 `Library`、`Temp`、`Logs`、`UserSettings` 或构建产物。
- 美术、音频、模型、视频和字体等大文件由 Git LFS 管理。

## 当前范围

项目当前保留 Unity 示例场景和默认产品名 `My project`，GitHub 仓库名及项目代号为 `yuying`。