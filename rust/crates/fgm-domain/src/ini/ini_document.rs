#[derive(Clone, Debug)]
enum LineKind {
    Raw,
    Section(String),
    Pair {
        section: String,
        key: String,
        value: String,
    },
    Blank,
}

#[derive(Clone, Debug)]
struct Line {
    text: String,
    kind: LineKind,
}

/// Line-preserving INI document. Unknown lines and comments survive edits.
pub struct IniDocument {
    lines: Vec<Line>,
}

impl IniDocument {
    pub fn parse(text: &str) -> Self {
        let text = text.strip_prefix('\u{feff}').unwrap_or(text);
        let normalized = text.replace("\r\n", "\n").replace('\r', "\n");
        let mut raw_lines: Vec<&str> = normalized.split('\n').collect();
        while raw_lines.last().is_some_and(|line| line.is_empty()) {
            raw_lines.pop();
        }

        let mut lines = Vec::with_capacity(raw_lines.len());
        let mut section = String::new();

        for raw in raw_lines {
            if raw.is_empty() {
                lines.push(Line {
                    text: raw.to_string(),
                    kind: LineKind::Blank,
                });
                continue;
            }

            let trimmed = raw.trim_start();
            if trimmed.starts_with(';') || trimmed.starts_with('#') {
                lines.push(Line {
                    text: raw.to_string(),
                    kind: LineKind::Raw,
                });
                continue;
            }

            if trimmed.starts_with('[') && trimmed.ends_with(']') {
                let name = trimmed[1..trimmed.len() - 1].trim().to_string();
                section = name.clone();
                lines.push(Line {
                    text: raw.to_string(),
                    kind: LineKind::Section(name),
                });
                continue;
            }

            if let Some(separator) = raw.find('=') {
                let key = raw[..separator].trim();
                if !key.is_empty() {
                    lines.push(Line {
                        text: raw.to_string(),
                        kind: LineKind::Pair {
                            section: section.clone(),
                            key: key.to_string(),
                            value: raw[separator + 1..].trim().to_string(),
                        },
                    });
                    continue;
                }
            }

            lines.push(Line {
                text: raw.to_string(),
                kind: LineKind::Raw,
            });
        }

        Self { lines }
    }

    pub fn get_value(&self, section: &str, key: &str) -> Option<&str> {
        self.lines.iter().rev().find_map(|line| match &line.kind {
            LineKind::Pair {
                section: candidate_section,
                key: candidate_key,
                value,
            } if candidate_section.eq_ignore_ascii_case(section)
                && candidate_key.eq_ignore_ascii_case(key) =>
            {
                Some(value.as_str())
            }
            _ => None,
        })
    }

    pub fn set_value(&mut self, section: &str, key: &str, value: &str) {
        if let Some(index) = self.find_last_pair(section, key) {
            let original_key = match &self.lines[index].kind {
                LineKind::Pair {
                    key: original_key, ..
                } => original_key.clone(),
                _ => key.to_string(),
            };
            self.lines[index].text = format!("{original_key}={value}");
            self.lines[index].kind = LineKind::Pair {
                section: section.to_string(),
                key: original_key,
                value: value.to_string(),
            };
            return;
        }

        let pair = Line {
            text: format!("{key}={value}"),
            kind: LineKind::Pair {
                section: section.to_string(),
                key: key.to_string(),
                value: value.to_string(),
            },
        };

        if let Some(section_index) = self.find_last_section(section) {
            let mut insert_at = section_index + 1;
            for (index, line) in self.lines.iter().enumerate().skip(section_index + 1) {
                match &line.kind {
                    LineKind::Section(_) => break,
                    LineKind::Pair {
                        section: candidate, ..
                    } if candidate.eq_ignore_ascii_case(section) => {
                        insert_at = index + 1;
                    }
                    _ => {}
                }
            }
            self.lines.insert(insert_at, pair);
            return;
        }

        if !section.is_empty() {
            if self.lines.last().is_some_and(|line| !line.text.is_empty()) {
                self.lines.push(Line {
                    text: String::new(),
                    kind: LineKind::Blank,
                });
            }
            self.lines.push(Line {
                text: format!("[{section}]"),
                kind: LineKind::Section(section.to_string()),
            });
        }
        self.lines.push(pair);
    }

    pub fn remove_value(&mut self, section: &str, key: &str) {
        self.lines.retain(|line| match &line.kind {
            LineKind::Pair {
                section: candidate_section,
                key: candidate_key,
                ..
            } => {
                !(candidate_section.eq_ignore_ascii_case(section)
                    && candidate_key.eq_ignore_ascii_case(key))
            }
            _ => true,
        });
    }

    pub fn to_text(&self, line_ending: &str) -> String {
        if self.lines.is_empty() {
            return String::new();
        }

        let mut text = self
            .lines
            .iter()
            .map(|line| line.text.as_str())
            .collect::<Vec<_>>()
            .join(line_ending);
        text.push_str(line_ending);
        text
    }

    fn find_last_pair(&self, section: &str, key: &str) -> Option<usize> {
        self.lines
            .iter()
            .enumerate()
            .rev()
            .find_map(|(index, line)| match &line.kind {
                LineKind::Pair {
                    section: candidate_section,
                    key: candidate_key,
                    ..
                } if candidate_section.eq_ignore_ascii_case(section)
                    && candidate_key.eq_ignore_ascii_case(key) =>
                {
                    Some(index)
                }
                _ => None,
            })
    }

    fn find_last_section(&self, section: &str) -> Option<usize> {
        self.lines
            .iter()
            .enumerate()
            .rev()
            .find_map(|(index, line)| match &line.kind {
                LineKind::Section(name) if name.eq_ignore_ascii_case(section) => Some(index),
                _ => None,
            })
    }
}
