#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
从门禁设备批量查询人员列表（ISAPI UserInfo/Search，等价于 SDK 的 NET_DVR_GetPersonList），
写入 backend/config/EmployeeConfig.json。

用法:
  python sync_employee_config.py
  python sync_employee_config.py --device-config ../config/DeviceConfig.json
"""

from __future__ import annotations

import argparse
import json
import ssl
import sys
import uuid
from datetime import datetime
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
import urllib.request
from urllib.request import HTTPDigestAuthHandler, HTTPPasswordMgrWithDefaultRealm, Request


DEFAULT_MAX_RESULTS = 30
DEFAULT_HTTP_PORT = 80
DEFAULT_HTTPS_PORT = 443


def resolve_paths(device_config: str | None, employee_config: str | None) -> tuple[Path, Path]:
    script_dir = Path(__file__).resolve().parent
    config_dir = script_dir.parent / "config"

    device_path = Path(device_config).resolve() if device_config else config_dir / "DeviceConfig.json"
    employee_path = Path(employee_config).resolve() if employee_config else config_dir / "EmployeeConfig.json"
    return device_path, employee_path


def load_device_config(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        raise FileNotFoundError(f"设备配置文件不存在: {path}")

    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)

    devices = data.get("devices") or []
    enabled = []
    for item in devices:
        if not isinstance(item, dict):
            continue
        if item.get("enabled") is False:
            continue
        ip = str(item.get("ip") or "").strip()
        if not ip:
            continue
        enabled.append(item)
    return enabled


def create_digest_opener(username: str, password: str, base_url: str):
    password_mgr = HTTPPasswordMgrWithDefaultRealm()
    password_mgr.add_password(None, base_url, username, password)
    handlers = [HTTPDigestAuthHandler(password_mgr)]
    return urllib.request.build_opener(*handlers)


def post_json(opener, url: str, payload: dict[str, Any], timeout: int) -> dict[str, Any]:
    body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    request = Request(
        url,
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with opener.open(request, timeout=timeout) as response:
        text = response.read().decode("utf-8", errors="replace").strip()
    if not text:
        return {}
    return json.loads(text)


def normalize_user_info_block(block: Any) -> list[dict[str, Any]]:
    if block is None:
        return []
    if isinstance(block, list):
        return [x for x in block if isinstance(x, dict)]
    if isinstance(block, dict):
        return [block]
    return []


def fetch_person_list(device: dict[str, Any], timeout: int, use_https: bool) -> list[dict[str, Any]]:
    ip = str(device.get("ip") or "").strip()
    username = str(device.get("userName") or device.get("username") or "admin").strip()
    password = str(device.get("password") or "")
    http_port = int(device.get("httpPort") or device.get("http_port") or (DEFAULT_HTTPS_PORT if use_https else DEFAULT_HTTP_PORT))

    scheme = "https" if use_https else "http"
    base_url = f"{scheme}://{ip}:{http_port}"
    search_url = f"{base_url}/ISAPI/AccessControl/UserInfo/Search?format=json"

    opener = create_digest_opener(username, password, base_url)
    if use_https:
        opener.add_handler(HTTPSNoVerifyHandler())

    search_id = uuid.uuid4().hex
    position = 0
    persons: list[dict[str, Any]] = []
    now = datetime.now().strftime("%Y-%m-%dT%H:%M:%S")
    remark = f"设备同步:{ip}"

    while True:
        payload = {
            "UserInfoSearchCond": {
                "searchID": search_id,
                "searchResultPosition": position,
                "maxResults": DEFAULT_MAX_RESULTS,
            }
        }

        try:
            result = post_json(opener, search_url, payload, timeout)
        except HTTPError as exc:
            raise RuntimeError(f"HTTP {exc.code}: {exc.reason}") from exc
        except URLError as exc:
            raise RuntimeError(str(exc.reason or exc)) from exc
        except json.JSONDecodeError as exc:
            raise RuntimeError("设备返回非 JSON 响应") from exc

        search = result.get("UserInfoSearch") or result.get("userInfoSearch") or {}
        status = str(search.get("responseStatusStrg") or search.get("statusString") or "").upper()
        if status == "NO MATCHES":
            break

        users = normalize_user_info_block(search.get("UserInfo") or search.get("userInfo"))
        if not users:
            break

        for user in users:
            employee_no = str(user.get("employeeNo") or user.get("employeeNO") or "").strip()
            name = str(user.get("name") or user.get("personName") or "").strip()
            card_no = str(user.get("cardNo") or user.get("cardNO") or "").strip()
            if not employee_no and not name and not card_no:
                continue

            key = employee_no or card_no
            persons.append(
                {
                    "employeeId": key,
                    "employeeNo": employee_no or key,
                    "name": name,
                    "card_id": card_no,
                    "status": "在职",
                    "remarks": remark,
                    "createDate": now,
                    "updateDate": now,
                }
            )

        num_of_matches = int(search.get("numOfMatches") or len(users) or 0)
        if num_of_matches <= 0:
            break

        position += num_of_matches
        total_matches = int(search.get("totalMatches") or 0)
        if total_matches > 0 and position >= total_matches:
            break

    return persons


class HTTPSNoVerifyHandler(urllib.request.HTTPSHandler):
    def __init__(self):
        context = ssl.create_default_context()
        context.check_hostname = False
        context.verify_mode = ssl.CERT_NONE
        super().__init__(context=context)


def merge_persons(all_persons: list[dict[str, Any]]) -> list[dict[str, Any]]:
    merged: dict[str, dict[str, Any]] = {}
    for person in all_persons:
        key = str(person.get("employeeId") or person.get("employeeNo") or person.get("card_id") or "").strip()
        if not key:
            continue
        merged[key.lower()] = person
    return sorted(merged.values(), key=lambda x: str(x.get("employeeId") or ""))


def save_employee_config(path: Path, employees: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as f:
        json.dump(employees, f, ensure_ascii=False, indent=2)
        f.write("\n")


def main() -> int:
    parser = argparse.ArgumentParser(description="从门禁设备同步人员到 EmployeeConfig.json")
    parser.add_argument("--device-config", help="DeviceConfig.json 路径")
    parser.add_argument("--employee-config", help="EmployeeConfig.json 输出路径")
    parser.add_argument("--timeout", type=int, default=15, help="HTTP 超时秒数，默认 15")
    parser.add_argument("--https", action="store_true", help="使用 HTTPS（默认 HTTP 80 端口）")
    args = parser.parse_args()

    try:
        device_path, employee_path = resolve_paths(args.device_config, args.employee_config)
        devices = load_device_config(device_path)
    except Exception as exc:
        print(f"[错误] {exc}", file=sys.stderr)
        return 1

    if not devices:
        print("[跳过] 没有已启用的门禁设备")
        return 0

    print(f"设备配置: {device_path}")
    print(f"输出文件: {employee_path}")
    print(f"待同步设备: {len(devices)} 台")

    all_persons: list[dict[str, Any]] = []
    success_count = 0

    for device in devices:
        ip = device.get("ip")
        label = device.get("name") or device.get("deviceName") or ip
        try:
            persons = fetch_person_list(device, timeout=args.timeout, use_https=args.https)
            all_persons.extend(persons)
            success_count += 1
            print(f"[成功] {label} ({ip}) -> {len(persons)} 人")
        except Exception as exc:
            print(f"[失败] {label} ({ip}): {exc}", file=sys.stderr)

    if success_count == 0:
        print("[错误] 所有设备同步失败", file=sys.stderr)
        return 1

    employees = merge_persons(all_persons)
    if not employees:
        print("[错误] 未获取到任何人员记录", file=sys.stderr)
        return 1

    save_employee_config(employee_path, employees)
    print(f"[完成] 已写入 {len(employees)} 条人员记录 -> {employee_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
