// 看板を円柱状に曲げる（物体の座標で x を円周に沿わせる）。
// 見る人は -z 側にいるので、両端が見る人の方へ回り込む（半円に包まれる感じ）。radius<=0 なら何もしない
float3 FuwaBend(float3 p, float radius)
{
    if (radius <= 0.0001) return p;
    float a = p.x / radius;
    float3 q = p;
    q.x = radius * sin(a);
    q.z = p.z - radius * (1.0 - cos(a));
    return q;
}
