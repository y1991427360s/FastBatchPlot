import asyncio
import json
import sys
from pathlib import Path
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = Path(__file__).resolve().parent
FILES = ['src/FastBatchPlot.CadBridge/CadPageConsistency.cs', 'src/FastBatchPlot.UiChecks/PageConsistencyChecks.cs']
PROMPT = '''完成一个边界明确的小功能：PDF 页后来源核验的空标记防护及异常测试。
项目已经有主体实现 src/FastBatchPlot.CadBridge/CadPageConsistency.cs，调用入口在 BatchPlotForm.TaskRun.cs；离线回归文件 src/FastBatchPlot.UiChecks/PageConsistencyChecks.cs。
只允许修改 CadPageConsistency.cs 和 PageConsistencyChecks.cs 两个文件。可读取项目其他文件帮助理解。
实现要求：实现 ICadRevisionHost 或 ICadPlotResourceHost 的宿主返回 null、空字符串或纯空白时不能绕过校验，构造时就应拒绝；没有实现这些可选接口的兼容宿主仍可使用。Verify 时接口异常或返回空标记必须失败，失败说明应明确已有文件保留、不得记成功。保留现有宿主实例比较和快照隔离。
补充真实有效的离线测试：两个接口空标记（null/空串/空白）；页后接口抛错；Verify 多次仍按最初快照比较；无接口宿主兼容。可在 PageConsistencyChecks.cs 新建私有假宿主/打印器，避免改其他文件。
可以执行 dotnet run --project src/FastBatchPlot.UiChecks -c Release 验证（运行离线假宿主，不连接 CAD）。不要打包、安装、启动或连接 CAD，不操作 COM 或真实 CAD，不修改其他文件、不调用其他智能体。主智能体将独立复核测试和构建。
只用简体中文报告实际修改、测试结果和未完成项。'''

def value(result):
    if result.isError:
        raise RuntimeError(str(result.content))
    return json.loads(next(c.text for c in result.content if c.type == 'text'))

async def main():
    global PROMPT
    patch_mode = '--patch-only' in sys.argv
    if patch_mode:
        PROMPT = PROMPT.replace('只允许修改 CadPageConsistency.cs 和 PageConsistencyChecks.cs 两个文件。可读取项目其他文件帮助理解。','只返回可应用的 unified diff 补丁，不调用任何工具、不读写文件、不执行命令。源码如下，信息足够，直接编写补丁。').replace('可以执行 dotnet run --project src/FastBatchPlot.UiChecks -c Release 验证（运行离线假宿主，不连接 CAD）。','不要执行验证，由主智能体测试。')
        for name in FILES:
            PROMPT += '\n文件：'+name+'\n'+(ROOT/name).read_text(encoding='utf-8-sig')
        PROMPT += '\n测试文件只需增加内部假宿主类；可使用完整 ICadHost 接口时，下面提供接口定义。\n'+(ROOT/'src/FastBatchPlot.CadBridge/ICadHost.cs').read_text(encoding='utf-8-sig')
    baseline = {f:(ROOT/f).read_text(encoding='utf-8-sig') for f in FILES}
    (EVIDENCE/('phase42-antigravity-patch-before.json' if patch_mode else 'phase42-antigravity-before.json')).write_text(json.dumps(baseline,ensure_ascii=False),encoding='utf-8')
    parameters=StdioServerParameters(command=sys.executable,args=[str(Path.home()/'.codex/tools/antigravity-mcp/server.py')])
    async with stdio_client(parameters) as (read,write):
        async with ClientSession(read,write) as session:
            await session.initialize()
            print(json.dumps(value(await session.call_tool('agy_health',{})),ensure_ascii=False),flush=True)
            job=value(await session.call_tool('agy_start',{'prompt':PROMPT,'cwd':str(ROOT),'mode':'accept-edits','timeout_seconds':480}))
            print(json.dumps(job,ensure_ascii=False),flush=True)
            while job['status']=='running':
                job=value(await session.call_tool('agy_status',{'job_id':job['id'],'wait_seconds':25}))
                (EVIDENCE/('phase42-antigravity-patch-result.json' if patch_mode else 'phase42-antigravity-result.json')).write_text(json.dumps(job,ensure_ascii=False,indent=2),encoding='utf-8')
                print(json.dumps({k:job.get(k) for k in ['id','status','pid','response','error']},ensure_ascii=False),flush=True)

if __name__=='__main__':
    asyncio.run(main())
