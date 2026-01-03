#!/usr/bin/env python3

import typing
import json
import logging
import re
from pydash import py_

from file import FluentFile
from fluentast import FluentAstAbstract
from project import Project
from fluent.syntax import ast, FluentParser, FluentSerializer

logging.basicConfig(level=logging.INFO)


def extract_text_from_pattern(pattern: ast.Pattern) -> str:
    """Извлекает текстовое значение из Fluent Pattern."""
    if not pattern or not pattern.elements:
        return ""
    
    text_parts = []
    for element in pattern.elements:
        if isinstance(element, ast.TextElement):
            text_parts.append(element.value)
        elif isinstance(element, ast.Placeable):
            # Для плейсхолдеров добавляем их представление
            if isinstance(element.expression, ast.VariableReference):
                text_parts.append(f"{{${element.expression.id.name}}}")
            elif isinstance(element.expression, ast.MessageReference):
                text_parts.append(f"{{{element.expression.id.name}}}")
            elif isinstance(element.expression, ast.FunctionReference):
                text_parts.append(f"{{{element.expression.id.name}()}}")
            elif isinstance(element.expression, ast.SelectExpression):
                text_parts.append("{select}")
            else:
                text_parts.append("{...}")
    
    return "".join(text_parts).strip()


def get_message_value(message: ast.Message) -> str:
    """Получает текстовое значение из Message."""
    if not message.value:
        return ""
    return extract_text_from_pattern(message.value)


def get_term_value(term: ast.Term) -> str:
    """Получает текстовое значение из Term."""
    if not term.value:
        return ""
    return extract_text_from_pattern(term.value)


def normalize_text(text: str) -> str:
    """Нормализует текст для сравнения (убирает лишние пробелы)."""
    if not text:
        return ""
    # Заменяем множественные пробелы на один
    text = re.sub(r'\s+', ' ', text)
    return text.strip()


def has_cyrillic(text: str) -> bool:
    """Проверяет, содержит ли текст кириллицу."""
    return bool(re.search(r'[а-яА-ЯёЁ]', text))


def has_english_words(text: str) -> bool:
    """Проверяет, содержит ли текст английские слова (эвристика)."""
    # Ищем слова, состоящие из латинских букв длиной 3+ символов
    return bool(re.search(r'\b[a-zA-Z]{3,}\b', text))


def is_identical_value(en_value: str, ru_value: str) -> bool:
    """Проверяет, идентичны ли значения после нормализации."""
    en_norm = normalize_text(en_value)
    ru_norm = normalize_text(ru_value)
    return en_norm == ru_norm and en_norm != ""


def is_partially_untranslated(ru_value: str) -> bool:
    """Эвристическая проверка на частично непереведённый текст."""
    if not ru_value:
        return False
    
    # Если нет кириллицы, но есть английские слова - вероятно непереведено
    if not has_cyrillic(ru_value) and has_english_words(ru_value):
        return True
    
    # Если есть и кириллица, и английские слова - возможно частично переведено
    if has_cyrillic(ru_value) and has_english_words(ru_value):
        # Проверяем, не является ли это техническими терминами (короткие слова)
        # Игнорируем очень короткие английские слова (2 символа)
        long_english_words = re.findall(r'\b[a-zA-Z]{4,}\b', ru_value)
        if len(long_english_words) > 0:
            return True
    
    return False


def get_priority(file_path: str, key: str) -> str:
    """Определяет приоритет перевода на основе пути к файлу и ключа."""
    high_priority_paths = [
        'ui/', 'commands/', 'shell.', 'connection-messages.', 
        'tips.', 'generic.', 'alerts/', 'chat/'
    ]
    
    for high_path in high_priority_paths:
        if high_path in file_path:
            return "high"
    
    if 'error' in key.lower() or 'fail' in key.lower() or 'invalid' in key.lower():
        return "high"
    
    return "medium"


class UntranslatedFinder:
    def __init__(self, project: Project):
        self.project = project
        self.untranslated_items = []
        
    def find_message_by_id(self, parsed: ast.Resource, message_id: str) -> typing.Optional[ast.Message]:
        """Находит сообщение по ID в распарсенном ресурсе."""
        for element in parsed.body:
            if isinstance(element, ast.Message) and element.id.name == message_id:
                return element
            elif isinstance(element, ast.Term) and element.id.name == message_id:
                # Для Term тоже возвращаем как Message (упрощение)
                return None
        return None
    
    def find_term_by_id(self, parsed: ast.Resource, term_id: str) -> typing.Optional[ast.Term]:
        """Находит термин по ID в распарсенном ресурсе."""
        for element in parsed.body:
            if isinstance(element, ast.Term) and element.id.name == term_id:
                return element
        return None
    
    def get_all_message_ids(self, parsed: ast.Resource) -> typing.List[str]:
        """Получает все ID сообщений из распарсенного ресурса."""
        ids = []
        for element in parsed.body:
            if isinstance(element, ast.Message):
                ids.append(element.id.name)
            elif isinstance(element, ast.Term):
                ids.append(element.id.name)
        return ids
    
    def compare_files(self, en_file: FluentFile, ru_file: FluentFile):
        """Сравнивает два файла и находит непереведённые строки."""
        try:
            en_parsed = en_file.read_parsed_data()
            ru_parsed = ru_file.read_parsed_data()
        except Exception as e:
            logging.warning(f"Ошибка при парсинге файлов {en_file.full_path} или {ru_file.full_path}: {e}")
            return
        
        en_message_ids = self.get_all_message_ids(en_parsed)
        ru_message_ids = self.get_all_message_ids(ru_parsed)
        
        relative_path = ru_file.get_relative_path(self.project.ru_locale_dir_path)
        
        # Проверяем все сообщения из английского файла
        for message_id in en_message_ids:
            en_message = self.find_message_by_id(en_parsed, message_id)
            en_term = self.find_term_by_id(en_parsed, message_id)
            
            if not en_message and not en_term:
                continue
            
            # Получаем английское значение
            if en_message:
                en_value = get_message_value(en_message)
            else:
                en_value = get_term_value(en_term)
            
            if not en_value:
                continue
            
            # Проверяем, есть ли этот ключ в русском файле
            ru_message = self.find_message_by_id(ru_parsed, message_id)
            ru_term = self.find_term_by_id(ru_parsed, message_id)
            
            if not ru_message and not ru_term:
                # Отсутствующий ключ
                self.untranslated_items.append({
                    "file_path": relative_path,
                    "key": message_id,
                    "en_value": en_value,
                    "ru_value": None,
                    "type": "missing",
                    "priority": get_priority(relative_path, message_id)
                })
                continue
            
            # Получаем русское значение
            if ru_message:
                ru_value = get_message_value(ru_message)
            else:
                ru_value = get_term_value(ru_term)
            
            # Проверяем на идентичность
            if is_identical_value(en_value, ru_value):
                self.untranslated_items.append({
                    "file_path": relative_path,
                    "key": message_id,
                    "en_value": en_value,
                    "ru_value": ru_value,
                    "type": "identical",
                    "priority": get_priority(relative_path, message_id)
                })
                continue
            
            # Проверяем на частичный перевод
            if is_partially_untranslated(ru_value):
                self.untranslated_items.append({
                    "file_path": relative_path,
                    "key": message_id,
                    "en_value": en_value,
                    "ru_value": ru_value,
                    "type": "partial",
                    "priority": get_priority(relative_path, message_id)
                })
    
    def execute(self):
        """Выполняет поиск непереведённых строк во всех файлах."""
        logging.info("Получение списка файлов...")
        en_files = self.project.get_fluent_files_by_dir(self.project.en_locale_dir_path)
        ru_files = self.project.get_fluent_files_by_dir(self.project.ru_locale_dir_path)
        
        # Создаём словарь для быстрого поиска русских файлов по относительному пути
        ru_files_dict = {}
        for ru_file in ru_files:
            rel_path = ru_file.get_relative_path(self.project.ru_locale_dir_path)
            ru_files_dict[rel_path] = ru_file
        
        logging.info(f"Найдено {len(en_files)} английских файлов и {len(ru_files)} русских файлов")
        
        processed = 0
        for en_file in en_files:
            en_rel_path = en_file.get_relative_path(self.project.en_locale_dir_path)
            
            # Пропускаем файлы, которые находятся в специфичных директориях, которых нет в ru-RU
            # (например, ss14-ru/prototypes в en-US может не иметь аналога в ru-RU)
            if 'ss14-ru/prototypes' in en_file.full_path:
                continue
            
            # Ищем соответствующий русский файл
            ru_file_path = en_file.full_path.replace('en-US', 'ru-RU')
            ru_file = FluentFile(ru_file_path) if ru_file_path in [f.full_path for f in ru_files] else None
            
            if not ru_file or not ru_file.full_path in [f.full_path for f in ru_files]:
                # Файл существует только в английской версии
                logging.debug(f"Русский файл не найден для {en_rel_path}")
                continue
            
            self.compare_files(en_file, ru_file)
            processed += 1
            
            if processed % 100 == 0:
                logging.info(f"Обработано {processed} файлов...")
        
        logging.info(f"Обработка завершена. Найдено {len(self.untranslated_items)} непереведённых строк")
    
    def generate_report(self, output_path: str):
        """Генерирует JSON отчёт с результатами."""
        import os
        # Создаём директорию, если её нет
        os.makedirs(os.path.dirname(output_path), exist_ok=True)
        
        # Группируем по файлам
        grouped_by_file = py_.group_by(self.untranslated_items, 'file_path')
        
        report = {
            "summary": {
                "total_untranslated": len(self.untranslated_items),
                "by_type": {
                    "missing": len([x for x in self.untranslated_items if x["type"] == "missing"]),
                    "identical": len([x for x in self.untranslated_items if x["type"] == "identical"]),
                    "partial": len([x for x in self.untranslated_items if x["type"] == "partial"])
                },
                "by_priority": {
                    "high": len([x for x in self.untranslated_items if x["priority"] == "high"]),
                    "medium": len([x for x in self.untranslated_items if x["priority"] == "medium"])
                },
                "files_affected": len(grouped_by_file)
            },
            "files": []
        }
        
        # Добавляем данные по файлам
        for file_path, items in grouped_by_file.items():
            file_data = {
                "file_path": file_path,
                "count": len(items),
                "untranslated": items
            }
            report["files"].append(file_data)
        
        # Сортируем файлы по количеству непереведённых строк
        report["files"].sort(key=lambda x: x["count"], reverse=True)
        
        # Сохраняем отчёт
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(report, f, ensure_ascii=False, indent=2)
        
        logging.info(f"Отчёт сохранён в {output_path}")


def main():
    import os
    import pathlib
    
    # Определяем базовый путь проекта (где находится Resources/Locale)
    script_dir = pathlib.Path(__file__).parent.resolve()
    base_dir = script_dir.parent.parent.resolve()
    
    project = Project()
    # Исправляем base_dir_path, если он неправильный
    if not os.path.exists(project.en_locale_dir_path):
        # Пытаемся найти правильный путь
        potential_base = script_dir.parent.parent
        potential_locale = os.path.join(potential_base, "Resources", "Locale", "en-US")
        if os.path.exists(potential_locale):
            project.base_dir_path = str(potential_base)
            project.resources_dir_path = os.path.join(project.base_dir_path, 'Resources')
            project.locales_dir_path = os.path.join(project.resources_dir_path, 'Locale')
            project.ru_locale_dir_path = os.path.join(project.locales_dir_path, 'ru-RU')
            project.en_locale_dir_path = os.path.join(project.locales_dir_path, 'en-US')
    
    finder = UntranslatedFinder(project)
    
    logging.info("Начало поиска непереведённых строк...")
    finder.execute()
    
    output_path = os.path.join(base_dir, "Tools", "ss14_ru", "untranslated_report.json")
    finder.generate_report(output_path)
    
    # Выводим краткую статистику
    summary = {
        "total": len(finder.untranslated_items),
        "missing": len([x for x in finder.untranslated_items if x["type"] == "missing"]),
        "identical": len([x for x in finder.untranslated_items if x["type"] == "identical"]),
        "partial": len([x for x in finder.untranslated_items if x["type"] == "partial"]),
        "high_priority": len([x for x in finder.untranslated_items if x["priority"] == "high"])
    }
    
    print("\n" + "="*60)
    print("КРАТКАЯ СТАТИСТИКА")
    print("="*60)
    print(f"Всего непереведённых строк: {summary['total']}")
    print(f"  - Отсутствующие ключи: {summary['missing']}")
    print(f"  - Идентичные значения: {summary['identical']}")
    print(f"  - Частично непереведённые: {summary['partial']}")
    print(f"  - Высокий приоритет: {summary['high_priority']}")
    print("="*60)
    print(f"\nПолный отчёт сохранён в: {output_path}")


if __name__ == "__main__":
    main()

