using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Proc.Application.Common;
using Proc.Application.Control;
using Proc.Application.Documents;
using Proc.Application.Inventory;
using Proc.Application.MasterData;
using Proc.Application.Organization;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Organization;

namespace Proc.IntegrationTests;

/// <summary>
/// Thực nhận chiết khấu sản lượng gộp (lát G6b, design.md mục 18.25): tổng công ty ghi thực nhận cho kỳ đã kết thúc, chia phần cho công ty áp
/// dụng theo tỷ lệ doanh số kỳ; tình trạng kỳ, cảnh báo CKBT, dấu hiệu KSNB khi quá hạn còn thiếu; hủy có lý do; đính kèm bản ghi.
/// </summary>
[Collection(ApiCollection.Name)]
public class VolumeRebateReceiptTests
{
    private const string Url = "/api/volume-rebates";
    private readonly ProcApiFactory _factory;

    public VolumeRebateReceiptTests(ProcApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant()[..10];

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTime.Zone));

    private static DateOnly ThisMonth => new(Today.Year, Today.Month, 1);

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Title;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    [Fact]
    public async Task ThucNhan_KyDaKetThuc_ChiaPhanTheoDoanhSo_CanhBaoVaKsnbKhiThieu()
    {
        var userName = $"vq.{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(userName, ProcApiFactory.TestPassword, ["TCT"]);
        var head = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, "TCT");
        async Task<(CompanyDetailDto Company, HttpClient Client, Guid Warehouse)> CompanyAsync(string name)
        {
            // Kỳ mở từ 3 tháng trước để PO được ghi vào kỳ đã qua hạn ghi nhận.
            var company = await ReadAsync<CompanyDetailDto>(await head.PostAsJsonAsync("/api/companies",
                new CreateCompanyRequest(UniqueCode("Q"), name, null, null, null, null, ThisMonth.AddMonths(-3), "KT", "Kho tổng"), Ct));
            var client = await _factory.CreateClientInCompanyAsync(userName, ProcApiFactory.TestPassword, company.Code);
            var warehouse = (await client.GetFromJsonAsync<List<WarehouseDetailDto>>("/api/warehouses", Ct))!.Single().Id;
            return (company, client, warehouse);
        }

        var a = await CompanyAsync("Công ty A thực nhận gộp");
        var b = await CompanyAsync("Công ty B thực nhận gộp");
        var cai = (await head.GetFromJsonAsync<List<UomDto>>("/api/uoms", Ct))!.Single(u => u.Code == "CAI").Id;
        var category = await ReadAsync<ItemCategoryDetailDto>(await head.PostAsJsonAsync("/api/item-categories",
            new CreateItemCategoryRequest(null, $"Nhóm thực nhận gộp {Guid.NewGuid():N}", null, null), Ct));
        var item = (await ReadAsync<ItemDetailDto>(await head.PostAsJsonAsync("/api/items",
            new CreateItemRequest(null, "Gạch lát G6b", null, "Inventory", category.Id, cai, null, null, null, null, "10", null, false, false, null, null, []), Ct))).Id;
        var groupId = (await head.GetFromJsonAsync<List<SupplierGroupDto>>("/api/supplier-groups", Ct))!.Single(g => g.Code == "TN").Id;
        var supplier = await ReadAsync<SupplierDetailDto>(await head.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(null, groupId, "NCC gạch thực nhận gộp", null, false, null, null, null, null, null, null, null, null,
                "Approved", null, null, [], []), Ct));
        await _factory.FinishSupplierApprovalAsync(supplier.Id);
        foreach (var c in new[] { a.Client, b.Client })
            await ReadAsync<SupplierCompanyDto>(await c.PutAsJsonAsync($"/api/suppliers/{supplier.Id}/company", new SaveSupplierCompanyRequest(null, null, true), Ct));

        // Thỏa thuận theo tháng từ 3 tháng trước đến hết tháng này; bậc 100 cái 1% – 200 cái 2%. Ban duyệt → hiệu lực.
        var from = ThisMonth.AddMonths(-3);
        var draft = await ReadAsync<VolumeRebateDto>(await head.PostAsJsonAsync(Url, new SaveVolumeRebateRequest(
            "Chiết khấu gạch lát", supplier.Id, category.Id, from, ThisMonth.AddMonths(1).AddDays(-1), "Month", "Quantity", null,
            [a.Company.Id, b.Company.Id], null, [new(null, item, cai, null)], [new(100, 1), new(200, 2)]), Ct));
        await ReadAsync<VolumeRebateDto>(await head.PostAsync($"{Url}/{draft.Id}/submit", null, Ct));
        var boardRole = UniqueCode("B");
        await _factory.CreateRoleAsync(boardRole, Permissions.Pricing.VolumeRebateView, Permissions.Pricing.VolumeRebateApprove);
        var boardUser = $"vqb.{Guid.NewGuid():N}"[..16];
        await _factory.CreateScopedUserAsync(boardUser, "TCT", boardRole);
        var board = await _factory.CreateClientInCompanyAsync(boardUser, ProcApiFactory.TestPassword, "TCT");
        Assert.Equal("Active", (await ReadAsync<VolumeRebateDto>(
            await board.PostAsJsonAsync($"{Url}/{draft.Id}/approve", new ApprovalActionRequest(null), Ct))).Status);

        // Kỳ đầu: A 120 cái, B 100 cái × 10.000 → 220 cái, bậc 2: phải nhận 2.200.000 × 2% = 44.000.
        async Task PoAsync((CompanyDetailDto Company, HttpClient Client, Guid Warehouse) c, DateOnly date, decimal qty)
        {
            var po = await ReadAsync<PurchaseOrderDto>(await c.Client.PostAsJsonAsync("/api/purchase-orders",
                new SavePurchaseOrderRequest(date, supplier.Id, c.Warehouse, null, null, null, null, 0, null,
                    [new SavePurchaseOrderLineRequest(null, item, cai, qty, 10_000, 0, "10", null, null, null)]), Ct));
            await ReadAsync<PurchaseOrderDto>(await c.Client.PostAsync($"/api/purchase-orders/{po.Id}/submit", null, Ct));
            await _factory.FinishPurchaseOrderApprovalAsync(po.Id);
        }

        await PoAsync(a, from.AddDays(2), 120);
        await PoAsync(b, from.AddDays(4), 100);
        await PoAsync(a, Today, 50);

        var progress = await ReadAsync<VolumeRebateProgressDto>(await head.GetAsync($"{Url}/{draft.Id}/progress", Ct));
        var first = progress.Periods[0];
        Assert.Equal((true, 30), (progress.CanRecord, progress.GraceDays));
        Assert.Equal((44_000m, 0m, 44_000m, "Short", from.AddMonths(1).AddDays(29)),
            (first.RebateAmount, first.ReceivedAmount, first.ShortAmount, first.ReceiptStatus, first.DueDate));
        Assert.Equal("NotReached", progress.Periods[1].ReceiptStatus);
        Assert.Equal("NotDue", progress.Periods[^1].ReceiptStatus);

        // CKBT: kỳ hiện tại bình thường nhưng có một kỳ quá hạn thiếu thực nhận → vẫn là dòng cảnh báo.
        var row = (await ReadAsync<List<VolumeRebateTrackingRowDto>>(await head.GetAsync($"{Url}/tracking?onlyAlerts=true", Ct)))
            .Single(r => r.AgreementId == draft.Id);
        Assert.Equal((1, 44_000m), (row.UnreceivedPeriods, row.UnreceivedAmount));

        // KSNB: chưa ghi nhận đồng nào → Cao, tiền = 44.000, mở CKB.
        var due = first.DueDate;
        var anomalyUrl = $"/api/control/anomalies?from={due:yyyy-MM-dd}&to={Today:yyyy-MM-dd}";
        var sign = Assert.Single((await ReadAsync<AnomalyResult>(await head.GetAsync(anomalyUrl, Ct))).Items,
            i => i.Code == AnomalyCodes.UnrecordedVolumeRebate && i.TargetId == draft.Id);
        Assert.Equal((AnomalySeverities.High, "CKB", draft.No, 44_000m, due), (sign.Severity, sign.ScreenCode, sign.RefNo, sign.Amount, sign.Date));

        // Kỳ hiện tại chưa kết thúc → không ghi được (người dùng chốt 08/11/2026); ngày nhận phải sau cuối kỳ.
        SaveVolumeRebateReceiptRequest Receipt(DateOnly period, decimal amount, DateOnly? on = null) =>
            new(period, on ?? Today, amount, "DebtOffset", "BB-CK-01", "Đối chiếu công nợ");
        Assert.Contains("chưa kết thúc", await TitleAsync(await head.PostAsJsonAsync($"{Url}/{draft.Id}/receipts", Receipt(ThisMonth, 1_000), Ct)));
        Assert.Contains("sau ngày cuối kỳ", await TitleAsync(await head.PostAsJsonAsync($"{Url}/{draft.Id}/receipts",
            Receipt(from, 1_000, from.AddDays(5)), Ct)));
        // Kỳ không có doanh số → không có căn cứ chia.
        Assert.Contains("chưa có doanh số", await TitleAsync(await head.PostAsJsonAsync($"{Url}/{draft.Id}/receipts",
            Receipt(from.AddMonths(1), 1_000), Ct)));
        // Công ty áp dụng không ghi được.
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Client.PostAsJsonAsync($"{Url}/{draft.Id}/receipts", Receipt(from, 1_000), Ct)).StatusCode);

        // Ghi 30.000: A 30.000 × 1,2 ÷ 2,2 = 16.363,64; B 13.636,36.
        var receipt = await ReadAsync<VolumeRebateReceiptDto>(await head.PostAsJsonAsync($"{Url}/{draft.Id}/receipts", Receipt(from, 30_000), Ct));
        Assert.Equal((30_000m, "DebtOffset", from.AddMonths(1).AddDays(-1)), (receipt.Amount, receipt.Form, receipt.PeriodTo));
        Assert.Equal([(a.Company.Code, 16_363.64m), (b.Company.Code, 13_636.36m)],
            receipt.Shares.Select(s => (s.CompanyCode, s.Amount)).OrderBy(s => s.CompanyCode == a.Company.Code ? 0 : 1));

        // Đính kèm biên bản cho bản ghi thực nhận (ở tổng công ty).
        var form = new MultipartFormDataContent
        {
            { new StringContent("VolumeRebateReceipt"), "ownerType" },
            { new StringContent(receipt.Id.ToString()), "ownerId" },
            { new ByteArrayContent("bien ban"u8.ToArray()), "file", "bien-ban.txt" },
        };
        await ReadAsync<AttachmentDto>(await head.PostAsync("/api/attachments", form, Ct));

        // Thiếu một phần → KSNB mức Trung bình, còn 14.000.
        sign = Assert.Single((await ReadAsync<AnomalyResult>(await head.GetAsync(anomalyUrl, Ct))).Items,
            i => i.Code == AnomalyCodes.UnrecordedVolumeRebate && i.TargetId == draft.Id);
        Assert.Equal((AnomalySeverities.Medium, 14_000m), (sign.Severity, sign.Amount));

        // Công ty áp dụng thấy thực nhận gộp và chỉ phần của mình.
        var member = await ReadAsync<VolumeRebateProgressDto>(await a.Client.GetAsync($"{Url}/{draft.Id}/progress", Ct));
        Assert.Equal((false, 30_000m), (member.CanRecord, member.Periods[0].ReceivedAmount));
        Assert.Equal([(a.Company.Code, 16_363.64m)], member.Periods[0].Companies.Select(s => (s.CompanyCode, s.ReceivedShare)));
        Assert.Equal([a.Company.Code], Assert.Single(member.Receipts).Shares.Select(s => s.CompanyCode));

        // Nhận đủ phần còn lại → Đủ, hết cảnh báo; hủy bản ghi đó (bắt buộc lý do) → lại thiếu.
        var rest = await ReadAsync<VolumeRebateReceiptDto>(await head.PostAsJsonAsync($"{Url}/{draft.Id}/receipts", Receipt(from, 14_000), Ct));
        progress = await ReadAsync<VolumeRebateProgressDto>(await head.GetAsync($"{Url}/{draft.Id}/progress", Ct));
        Assert.Equal(("Received", 44_000m, 0m), (progress.Periods[0].ReceiptStatus, progress.Periods[0].ReceivedAmount, progress.Periods[0].ShortAmount));
        Assert.DoesNotContain((await ReadAsync<AnomalyResult>(await head.GetAsync(anomalyUrl, Ct))).Items,
            i => i.Code == AnomalyCodes.UnrecordedVolumeRebate && i.TargetId == draft.Id);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await head.PostAsJsonAsync($"{Url}/receipts/{rest.Id}/cancel", new CancelVolumeRebateRequest(" "), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await a.Client.PostAsJsonAsync($"{Url}/receipts/{rest.Id}/cancel", new CancelVolumeRebateRequest("x"), Ct)).StatusCode);
        var cancelled = await ReadAsync<VolumeRebateReceiptDto>(
            await head.PostAsJsonAsync($"{Url}/receipts/{rest.Id}/cancel", new CancelVolumeRebateRequest("Ghi trùng biên bản"), Ct));
        Assert.Equal((true, "Ghi trùng biên bản"), (cancelled.IsCancelled, cancelled.CancelReason));
        progress = await ReadAsync<VolumeRebateProgressDto>(await head.GetAsync($"{Url}/{draft.Id}/progress", Ct));
        Assert.Equal(("Short", 30_000m, 2), (progress.Periods[0].ReceiptStatus, progress.Periods[0].ReceivedAmount, progress.Receipts.Count));
    }
}
