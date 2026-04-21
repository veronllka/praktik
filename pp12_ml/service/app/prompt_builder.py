from .schemas import SimilarExample


def build_description_prompt(
    *,
    title: str,
    predicted_category: str,
    site_name: str | None,
    existing_description: str | None,
    similar_examples: list[SimilarExample],
) -> str:
    lines = [
        "Сгенерируй краткое деловое описание строительной задачи для карточки в системе планирования работ.",
        f"Название задачи: {title.strip()}",
        f"Предсказанная категория: {predicted_category}",
    ]

    if site_name and site_name.strip():
        lines.append(f"Объект: {site_name.strip()}")

    if existing_description and existing_description.strip():
        lines.append(f"Текущее описание для учета контекста: {existing_description.strip()}")

    if similar_examples:
        lines.append("Похожие примеры:")
        for index, example in enumerate(similar_examples[:3], start=1):
            lines.append(
                f"{index}. {example.title} - {example.reference_description}"
            )

    lines.extend(
        [
            "Требования к ответу:",
            "- Ответь только итоговым описанием без заголовков и markdown.",
            "- Пиши на русском языке.",
            "- Используй деловой и краткий стиль.",
            "- Дай 1-2 предложения.",
            "- Учитывай категорию задачи и похожие примеры.",
            "- Не упоминай сроки даты бюджет приоритеты и состав бригады если они не переданы явно.",
        ]
    )

    return "\n".join(lines)
