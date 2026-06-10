namespace ACSEventConsole.Infrastructure.Sdk
{
    public static class AcsEventTypeHelper
    {
        public static string GetMajorTypeString(uint dwMajor)
        {
            switch (dwMajor)
            {
                case CHCNetSDK.MAJOR_ALARM:
                    return "报警";
                case CHCNetSDK.MAJOR_EXCEPTION:
                    return "异常";
                case CHCNetSDK.MAJOR_OPERATION:
                    return "操作";
                case CHCNetSDK.MAJOR_EVENT:
                    return "事件";
                default:
                    return "未知类型(" + dwMajor + ")";
            }
        }

        public static string GetMinorTypeString(uint dwMajor, uint dwMinor)
        {
            switch (dwMajor)
            {
                case CHCNetSDK.MAJOR_ALARM:
                    return "未知报警类型(" + dwMinor + ")";
                case CHCNetSDK.MAJOR_EXCEPTION:
                    return "未知异常类型(" + dwMinor + ")";
                case CHCNetSDK.MAJOR_OPERATION:
                    return "未知操作类型(" + dwMinor + ")";
                case CHCNetSDK.MAJOR_EVENT:
                    return GetEventMinorTypeString(dwMinor);
                default:
                    return "未知次类型(" + dwMinor + ")";
            }
        }

        private static string GetEventMinorTypeString(uint dwMinor)
        {
            switch (dwMinor)
            {
                case CHCNetSDK.MINOR_LEGAL_CARD_PASS:
                    return "合法卡通过";
                case CHCNetSDK.MINOR_CARD_AND_PSW_PASS:
                    return "刷卡加密码通过";
                case CHCNetSDK.MINOR_CARD_AND_PSW_FAIL:
                    return "刷卡加密码失败";
                case CHCNetSDK.MINOR_CARD_AND_PSW_TIMEOUT:
                    return "数卡加密码超时";
                case CHCNetSDK.MINOR_CARD_NO_RIGHT:
                    return "卡无权限";
                case CHCNetSDK.MINOR_CARD_INVALID_PERIOD:
                    return "卡不在有效期";
                case CHCNetSDK.MINOR_CARD_OUT_OF_DATE:
                    return "卡过期";
                case CHCNetSDK.MINOR_INVALID_CARD:
                    return "无效卡";
                case CHCNetSDK.MINOR_DOOR_OPEN_NORMAL:
                    return "门正常打开";
                case CHCNetSDK.MINOR_DOOR_CLOSE_NORMAL:
                    return "门正常关闭";
                case CHCNetSDK.MINOR_DOOR_OPEN_ABNORMAL:
                    return "门异常打开";
                case CHCNetSDK.MINOR_DOOR_OPEN_TIMEOUT:
                    return "门打开超时";
                case 75:
                    return "人脸认证通过";
                case 0:
                    return "全部事件";
                default:
                    return "未知事件类型(" + dwMinor + ")";
            }
        }

        public static string GetCardTypeString(byte byCardType)
        {
            switch (byCardType)
            {
                case 1:
                    return "普通卡";
                case 2:
                    return "残疾人卡";
                case 3:
                    return "黑名单卡";
                case 4:
                    return "巡更卡";
                case 5:
                    return "胁迫卡";
                default:
                    return byCardType > 0 ? "卡类型(" + byCardType + ")" : string.Empty;
            }
        }
    }
}
