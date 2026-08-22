namespace ServiceLib.Handler.Fmt;

public class SingboxFmt : BaseFmt
{
    public static List<ProfileItem>? ResolveFullArray(string strData, string? subRemarks)
    {
<<<<<<< HEAD
        var configObjects = JsonUtils.Deserialize<object[]>(strData);
        if (configObjects is not { Length: > 0 })
=======
        var jsonNode = JsonUtils.ParseJson(strData);
        return ResolveCommon(jsonNode, subRemarks, false);
    }

    public static List<ProfileItem> ResolveToCustomOutbound(string strData, string? subRemarks)
    {
        var jsonNode = JsonUtils.ParseJson(strData);
        return ResolveCommon(jsonNode, subRemarks, true);
    }

    private static List<ProfileItem> ResolveCommon(JsonNode? jsonNode, string? subRemarks, bool isOutbound)
    {
        if (jsonNode is JsonArray jsonArray)
        {
            return
            [
                .. jsonArray.Select(item => ResolveCommon(item, subRemarks, isOutbound))
                    .Where(list => list is { Count: > 0 })
                    .SelectMany(list => list),
            ];
        }
        if (jsonNode is not JsonObject jsonObject)
        {
            return [];
        }
        // Process the individual JSON object
        var profileList = new List<ProfileItem>();
        if (!isOutbound)
        {
            var fullProfile = ResolveFull(jsonObject, subRemarks);
            if (fullProfile is not null)
            {
                profileList.Add(fullProfile);
                return profileList;
            }
        }
        profileList.AddRange(ResolveFullToOutbound(jsonObject, subRemarks));
        if (profileList.Count != 0)
        {
            return profileList;
        }
        var outboundProfile = ResolveOutbound(jsonObject, subRemarks);
        if (outboundProfile is not null)
        {
            profileList.Add(outboundProfile);
        }
        return profileList;
    }

    private static ProfileItem? ResolveFull(JsonObject jsonObject, string? subRemarks)
    {
        if (jsonObject?["inbounds"] == null
            || jsonObject["outbounds"] == null)
>>>>>>> 26a86f9b (Fix (#10002))
        {
            return null;
        }

        List<ProfileItem> lstResult = [];
        foreach (var configObject in configObjects)
        {
            var objectString = JsonUtils.Serialize(configObject);
            var profileIt = ResolveFull(objectString, subRemarks);
            if (profileIt != null)
            {
                lstResult.Add(profileIt);
            }
        }
        return lstResult;
    }

    public static ProfileItem? ResolveFull(string strData, string? subRemarks)
    {
        var config = JsonUtils.ParseJson(strData);
        if (config?["inbounds"] == null
            || config["outbounds"] == null
            || config["route"] == null
            || config["dns"] == null)
        {
            return null;
        }

        var fileName = WriteAllText(strData);
        var profileItem = new ProfileItem
        {
            CoreType = ECoreType.sing_box,
            Address = fileName,
            Remarks = subRemarks ?? "singbox_custom"
        };

        return profileItem;
    }
}
