/// Canonical update locations and mirror expansion.
pub struct UpdateSource;

impl UpdateSource {
    pub const REPOSITORY: &'static str = "XiaoQAQ10086/FrameGen-Manager-Next";

    /// Manifest URLs, tried in order: raw GitHub then mirrors.
    pub fn manifest_urls() -> Vec<String> {
        let direct = format!(
            "https://raw.githubusercontent.com/{}/main/update.json",
            Self::REPOSITORY
        );
        let mut urls = vec![direct.clone()];
        urls.extend(
            Self::mirrors(&direct)
                .into_iter()
                .filter(|url| url != &direct),
        );
        urls
    }

    /// Mirrors for a GitHub URL, cheapest first, direct link last.
    pub fn mirrors(url: &str) -> Vec<String> {
        if !url.starts_with("https://github.com/")
            && !url.starts_with("https://raw.githubusercontent.com/")
        {
            return Vec::new();
        }
        vec![
            format!("https://ghfast.top/{url}"),
            format!("https://gh-proxy.com/{url}"),
            format!("https://ghproxy.net/{url}"),
            url.to_string(),
        ]
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn expands_github_links_and_keeps_direct_last() {
        let url = "https://github.com/owner/repo/releases/download/v1/Setup.exe";
        let expanded = UpdateSource::mirrors(url);
        assert_eq!(expanded.len(), 4);
        assert_eq!(expanded[0], format!("https://ghfast.top/{url}"));
        assert_eq!(expanded[3], url);
    }

    #[test]
    fn leaves_non_github_links_untouched() {
        assert!(UpdateSource::mirrors("https://example.com/file.zip").is_empty());
    }

    #[test]
    fn manifest_urls_start_with_raw_github() {
        let urls = UpdateSource::manifest_urls();
        assert!(urls[0].starts_with("https://raw.githubusercontent.com/"));
        assert!(urls.len() > 1);
    }
}
