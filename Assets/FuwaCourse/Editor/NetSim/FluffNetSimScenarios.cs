using System.Collections;
using System.Linq;
using UnityEngine;
using Haselab.NetSim;

// NetSim（複数クライアントの同期シミュレーター）用のテストシナリオ。try-netsim ブランチでお試し中。
// 実行例：Haselab.NetSim.NetSimScenarioRunner.RunWhenReady("fluff-explore", new Haselab.NetSim.NetSimConfig(), timeScale: 2);
// レポートは Logs/NetSim/ に出る
static class FluffNetSimScenarios
{
    // まずは様子見：2人入って、各クライアントにどんなエアガン・ゴーストが出ているかと、止まったUdonを書き出す
    [NetSimScenario("fluff-explore")]
    static IEnumerator Explore(NetSimScenarioContext ctx)
    {
        yield return ctx.AddClients(1);
        yield return ctx.Settle(3);
        ctx.R($"- clients: {NetSim.Clients.Count}");
        foreach (var c in NetSim.Clients)
        {
            var guns = c.goByPath.Keys.Where(k => k.Contains("FuwaBlower") && k.Count(ch => ch == '/') == 0).ToList();
            var ghosts = c.goByPath.Keys.Where(k => k.Contains("FuwaGhost") && k.Count(ch => ch == '/') <= 1).ToList();
            ctx.R($"- client {c.player.playerId} ({c.player.displayName}) master={c == ctx.Master}: guns=[{string.Join(", ", guns)}] ghosts=[{string.Join(", ", ghosts)}]");
        }
        ctx.R($"- halted: {string.Join(", ", NetSim.HaltedBehaviours())}");
        ctx.ReportConsistency("after join");
    }
}
