# yuying

`yuying` 是一个使用 Unity 制作的单机独立游戏项目。

## 开发环境

- Unity Editor：`2022.3.62f3`
- 版本控制：Git + Git LFS
- 远程仓库：<https://github.com/502jsui/yuying>

## 首次获取项目

1. 安装 [Git LFS](https://git-lfs.com/)。
2. 在终端执行：

   ```bash
   git lfs install
   git clone https://github.com/502jsui/yuying.git
   ```

3. 在 Unity Hub 中打开克隆后的目录。
4. 使用 Unity `2022.3.62f3` 打开项目，等待包和资源导入完成。
5. 打开 `Assets/Scenes/SampleScene.unity`，确认编辑器控制台没有报错。

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