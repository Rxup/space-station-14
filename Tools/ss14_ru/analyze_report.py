#!/usr/bin/env python3
import json
import os

report_path = os.path.join(os.path.dirname(__file__), "untranslated_report.json")

with open(report_path, 'r', encoding='utf-8') as f:
    data = json.load(f)

print("="*80)
print("АНАЛИЗ ОТЧЁТА О НЕПЕРЕВЕДЁННЫХ СТРОКАХ")
print("="*80)
print(f"\nОбщая статистика:")
print(f"  Всего непереведённых строк: {data['summary']['total_untranslated']}")
print(f"  Файлов затронуто: {data['summary']['files_affected']}")
print(f"\nПо типам:")
print(f"  - Отсутствующие ключи: {data['summary']['by_type']['missing']}")
print(f"  - Идентичные значения: {data['summary']['by_type']['identical']}")
print(f"  - Частично непереведённые: {data['summary']['by_type']['partial']}")
print(f"\nПо приоритетам:")
print(f"  - Высокий приоритет: {data['summary']['by_priority']['high']}")
print(f"  - Средний приоритет: {data['summary']['by_priority']['medium']}")

print("\n" + "="*80)
print("ТОП-20 ФАЙЛОВ С НАИБОЛЬШИМ КОЛИЧЕСТВОМ НЕПЕРЕВЕДЁННЫХ СТРОК")
print("="*80)
for i, file_data in enumerate(data['files'][:20], 1):
    print(f"{i:2}. {file_data['file_path']:60} - {file_data['count']:4} строк")

print("\n" + "="*80)
print("ФАЙЛЫ С ВЫСОКИМ ПРИОРИТЕТОМ (первые 20)")
print("="*80)
high_priority_files = {}
for file_data in data['files']:
    high_count = sum(1 for item in file_data['untranslated'] if item['priority'] == 'high')
    if high_count > 0:
        high_priority_files[file_data['file_path']] = high_count

sorted_high_priority = sorted(high_priority_files.items(), key=lambda x: x[1], reverse=True)
for i, (file_path, count) in enumerate(sorted_high_priority[:20], 1):
    print(f"{i:2}. {file_path:60} - {count:4} строк высокого приоритета")

